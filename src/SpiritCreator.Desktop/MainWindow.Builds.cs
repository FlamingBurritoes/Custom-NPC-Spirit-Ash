using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SpiritCreator.Desktop;

public sealed partial class MainWindow
{
    private static readonly FilePickerFileType BuildFileType = new("Spirit Creator build JSON") { Patterns = ["*.json"] };
    private string? _buildFileName;

    private async void SaveBuild(object? sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            var build = CaptureBuild();
            build.Validate();
            using var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Spirit Creator build",
                SuggestedFileName = _buildFileName ?? "Spirit Build.json",
                DefaultExtension = "json",
                FileTypeChoices = [BuildFileType],
                ShowOverwritePrompt = true
            });
            if (file is null)
                return;
            var path = file.TryGetLocalPath() ?? throw new IOException("Choose a local JSON file.");
            if (!Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Use a .json filename.");
            await Task.Run(() => build.Save(path));
            _buildFileName = Path.GetFileName(path);
            ShowBuildStatus($"Saved {_buildFileName}.");
        }
        catch (Exception ex) when (ExpectedFailure(ex))
        {
            ShowBuildStatus($"Could not save build: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void LoadBuild(object? sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            var presets = Path.Combine(AppContext.BaseDirectory, "saves");
            using var folder = Directory.Exists(presets)
                ? await StorageProvider.TryGetFolderFromPathAsync(presets) : null;
            var path = await Pick("Load Spirit Creator build", BuildFileType.Name, ["*.json"], folder);
            if (path is null)
                return;
            var build = await Task.Run(() => SpiritBuild.Load(path));
            ApplyBuild(build);
            _buildFileName = Path.GetFileName(path);
            ShowBuildStatus($"Loaded {_buildFileName}.");
        }
        catch (JsonException)
        {
            ShowBuildStatus("Could not load build: choose a valid Spirit Creator build JSON.");
        }
        catch (Exception ex) when (ExpectedFailure(ex))
        {
            ShowBuildStatus($"Could not load build: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowBuildStatus(string message)
    {
        BuildStatus.Text = message;
        BuildStatus.IsVisible = true;
    }

    private SpiritBuild CaptureBuild()
    {
        if (Weapons.SelectedItem is not WeaponChoice right || LeftWeapons.SelectedItem is not WeaponChoice left ||
            TargetAshBox.SelectedItem is not AshTargetChoice target)
            throw new InvalidDataException("Choose your weapons and replacement target first.");
        var attributes = ReadAttributes() ?? throw new InvalidDataException("Enter whole attributes from 10 to 99.");
        if (Hp.Value is not { } hp || Damage.Value is not { } damage ||
            hp != decimal.Truncate(hp) || damage != decimal.Truncate(damage))
            throw new InvalidDataException("Enter whole HP and damage percentages.");
        var (talismans, items, validQuantities) = ReadExtras();
        if (!validQuantities)
            throw new InvalidDataException("Enter a whole quantity for each selected item.");
        var armor = ArmorInputs.Select(input => (input.SelectedItem as ArmorChoice)?.Id ?? EquipmentCatalog.Empty).ToArray();
        return new()
        {
            Profile = _appearance with
            {
                WeaponId = right.Id, LeftWeaponId = left.Id, Attributes = attributes,
                Armor = new(armor[0], armor[1], armor[2], armor[3]),
                RightAshOfWarId = SelectedAsh(RightAsh), LeftAshOfWarId = SelectedAsh(LeftAsh),
                RightAffinity = SelectedAffinity(RightAffinity), LeftAffinity = SelectedAffinity(LeftAffinity),
                RightReinforcement = SelectedLevel(RightReinforcement), LeftReinforcement = SelectedLevel(LeftReinforcement),
                RightAmmoId = (RightAmmo.SelectedItem as AmmoChoice)?.Id, LeftAmmoId = (LeftAmmo.SelectedItem as AmmoChoice)?.Id,
                Shade = (ShadeBox.SelectedItem as ShadeChoice)?.Shade ?? CompanionShade.None,
                CombatPreset = SelectedPreset.Preset, SpellIds = SpellLoadout(right.Id).ToArray(),
                TalismanIds = talismans, Consumables = null,
                SummonName = SummonNameBox.Text?.Trim(), TargetAshId = target.GoodsId,
                TargetBuddyRowId = (TargetRowBox.SelectedItem as AshSpawnChoice)?.RowId,
                HpPercent = (int)hp, DamagePercent = (int)damage,
                OutgoingDamagePercent = OutgoingDamage.SelectedItem is OutgoingDamageChoice { Percent: var percent } &&
                    percent != SpiritProfile.StockOutgoingDamagePercent ? percent : null
            },
            QuickItems = items,
            OtherSpellLoadouts = _spellLoadouts.Where(entry => entry.Key != right.Id && SpellCatalog.ForWeapon(entry.Key).Count > 0)
                .ToDictionary(entry => entry.Key, entry => entry.Value.ToArray())
        };
    }

    private void ApplyBuild(SpiritBuild build)
    {
        build.Validate();
        var profile = build.Profile;
        _ready = false;
        try
        {
            _appearance = profile;
            _savePath = null;
            Characters.ItemsSource = null;
            Characters.SelectedIndex = -1;
            SaveLabel.Text = null;
            SaveLabel.IsVisible = false;
            foreach (var search in new[] { WeaponSearch, ArmorSearch, AshSearch, AmmoSearch, TalismanSearch, ItemSearch, SpellSearch, IncantationSearch })
                search.Text = "";
            ApplyEquipmentSearch();
            var inputs = StatInputs;
            var stats = profile.Attributes!.Values;
            for (var i = 0; i < inputs.Length; i++)
                inputs[i].Value = stats[i];

            Weapons.SelectedItem = EquipmentCatalog.Weapon(profile.WeaponId);
            LeftWeapons.SelectedItem = EquipmentCatalog.Weapon(profile.LeftWeaponId!.Value);
            RefreshWeaponOptions(Weapons, RightAsh, RightAffinity, RightReinforcement);
            RefreshWeaponOptions(LeftWeapons, LeftAsh, LeftAffinity, LeftReinforcement);
            SetWeaponOptions(Weapons, RightAsh, RightAffinity, RightReinforcement, RightAmmo,
                profile.RightAshOfWarId, profile.RightAffinity, profile.RightReinforcement, profile.RightAmmoId);
            SetWeaponOptions(LeftWeapons, LeftAsh, LeftAffinity, LeftReinforcement, LeftAmmo,
                profile.LeftAshOfWarId, profile.LeftAffinity, profile.LeftReinforcement, profile.LeftAmmoId);

            var armor = ArmorInputs;
            var armorIds = profile.Armor!.Ids;
            for (var i = 0; i < armor.Length; i++)
                armor[i].SelectedItem = EquipmentCatalog.ArmorFor((ArmorSlot)i).Single(choice => choice.Id == armorIds[i]);
            for (var i = 0; i < _talismans.Count; i++)
            {
                var id = profile.TalismanIds is { } ids && i < ids.Length ? ids[i] : EquipmentCatalog.Empty;
                _talismans[i].SelectedItem = ExtrasCatalog.Talismans.Single(choice => choice.Id == id);
            }
            for (var i = 0; i < _consumables.Count; i++)
            {
                var (box, quantity) = _consumables[i];
                var stack = build.QuickItems[i];
                box.SelectedItem = ExtrasCatalog.Consumables.Single(choice => choice.Id == (stack?.Id ?? EquipmentCatalog.Empty));
                quantity.Value = stack?.Quantity ?? 1;
            }

            PresetBox.SelectedItem = CombatPresetChoice.Options.Single(choice => choice.Preset == profile.CombatPreset);
            ShadeBox.SelectedItem = ShadeChoice.Options.Single(choice => choice.Shade == profile.Shade);
            var target = AshTargetCatalog.Options.Single(choice => choice.GoodsId == (profile.TargetAshId ?? AshTargetCatalog.DefaultGoodsId));
            TargetAshBox.SelectedItem = target;
            TargetRowBox.ItemsSource = target.Rows;
            TargetRowBox.SelectedItem = profile.TargetBuddyRowId is { } rowId
                ? target.Rows.Single(row => row.RowId == rowId)
                : target.Rows.Count == 1 ? target.Rows[0] : null;
            SummonNameBox.Text = profile.SummonName;
            Hp.Value = profile.HpPercent;
            Damage.Value = profile.DamagePercent;
            SelectOutgoingDamage(profile.OutgoingDamagePercent);
            _spellLoadouts.Clear();
            foreach (var (weapon, spells) in build.OtherSpellLoadouts ?? [])
                _spellLoadouts[weapon] = [.. spells];
            _spellLoadouts[profile.WeaponId] = [.. (profile.SpellIds ?? SpellCatalog.StarterIds(profile.WeaponId))];
            RefreshSpellPicker();
            _exportedProfile = null;
        }
        finally
        {
            _ready = true;
        }
        Refresh();
    }

    private static void SetWeaponOptions(ComboBox weapon, ComboBox ash, ComboBox affinity, ComboBox level, ComboBox ammo,
        int? ashId, WeaponAffinity weaponAffinity, int reinforcement, int? ammoId)
    {
        var id = SelectedWeapon(weapon).Id;
        ash.SelectedItem = AshOfWarCatalog.ForWeapon(id).Single(choice => choice.GemId == (ashId ?? -1));
        RefreshAffinities(id, ash, affinity);
        affinity.SelectedItem = WeaponUpgradeCatalog.ForWeapon(id, ashId).Single(choice => choice.Affinity == weaponAffinity);
        level.SelectedIndex = reinforcement;
        ammo.SelectedItem = ammoId is { } saved ? AmmunitionCatalog.All.Single(choice => choice.Id == saved)
            : AmmunitionCatalog.Default(AmmunitionCatalog.TypeForWeapon(id));
    }
}
