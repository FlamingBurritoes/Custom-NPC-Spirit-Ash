using System.Diagnostics;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SpiritCreator.Desktop;

public sealed partial class MainWindow : Window
{
    private string? _savePath;
    private string? _exportedProfile;
    private string? _regulation;
    private string? _me3;
    private bool _busy;
    private bool _ready;
    private readonly Dictionary<int, List<int>> _spellLoadouts = [];
    private readonly List<ComboBox> _talismans = [];
    private readonly List<(ComboBox Item, NumericUpDown Quantity)> _consumables = [];
    private NumericUpDown[] StatInputs => [Vigor, Mind, Endurance, Strength, Dexterity, Intelligence, Faith, Arcane];
    private ComboBox[] ArmorInputs => [HeadArmor, ChestArmor, HandsArmor, LegsArmor];
    private CombatPresetChoice SelectedPreset => PresetBox.SelectedItem as CombatPresetChoice ?? CombatPresetChoice.Options[0];
    private int RightWeaponId => (Weapons.SelectedItem as WeaponChoice)?.Id ?? EquipmentCatalog.Empty;

    public MainWindow()
    {
        InitializeComponent();
        Hp.Minimum = Damage.Minimum = SpiritProfile.MinPercent;
        Hp.Maximum = Damage.Maximum = SpiritProfile.MaxPercent;
        foreach (var input in StatInputs)
        {
            input.Minimum = NpcAttributes.Minimum;
            input.Maximum = NpcAttributes.Maximum;
            input.Value = NpcAttributes.Minimum;
            input.FormatString = "0";
            input.ClipValueToMinMax = true;
        }
        Weapons.ItemsSource = EquipmentCatalog.Weapons;
        LeftWeapons.ItemsSource = EquipmentCatalog.Weapons.Where(weapon => weapon.Kind != WeaponKind.Catalyst).ToArray();
        Weapons.SelectedItem = EquipmentCatalog.Weapon(EquipmentCatalog.Club);
        LeftWeapons.SelectedIndex = 0;
        RefreshWeaponOptions(Weapons, RightAsh, RightAffinity, RightReinforcement);
        RefreshWeaponOptions(LeftWeapons, LeftAsh, LeftAffinity, LeftReinforcement);
        ShadeBox.ItemsSource = ShadeChoice.Options;
        ShadeBox.SelectedIndex = 0;
        PresetBox.ItemsSource = CombatPresetChoice.Options;
        PresetBox.SelectedIndex = 0;
        var armorBoxes = ArmorInputs;
        foreach (var slot in Enum.GetValues<ArmorSlot>())
        {
            armorBoxes[(int)slot].ItemsSource = EquipmentCatalog.ArmorFor(slot);
            armorBoxes[(int)slot].SelectedIndex = 0;
        }
        _regulation = LocalPaths.FindStockRegulation();
        _me3 = LocalPaths.FindMe3();
        BuildExtrasControls();
        _ready = true;
        RefreshSpellPicker();
        Refresh();
    }

    private async void ImportSave(object? sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            using var folder = Directory.Exists(LocalPaths.SaveRoot)
                ? await StorageProvider.TryGetFolderFromPathAsync(LocalPaths.SaveRoot)
                : null;
            var path = await Pick("Open Elden Ring save or backup", "Elden Ring saves", ["*.sl2", "*.sl2.bak"], folder);
            if (path is null)
                return;
            Status.Text = "Reading character slots…";
            var characters = await Task.Run(() => SaveAppearance.ListCharacters(path));
            _savePath = path;
            _exportedProfile = null;
            SaveLabel.Text = Path.GetFileName(path);
            Characters.ItemsSource = characters;
            Characters.SelectedIndex = -1;
            Status.Text = characters.Count == 0
                ? "This save has no active characters. Select a different save."
                : $"Found {characters.Count} active {(characters.Count == 1 ? "character" : "characters")}. Choose a slot.";
        }
        catch (InvalidDataException ex)
        {
            Status.Text = ex.Message;
        }
        catch (Exception ex) when (ExpectedFailure(ex))
        {
            // I/O errors can contain the full save path, including the Steam ID.
            Status.Text = "Could not read the save. Check that it’s a local PC Elden Ring save, then try a backup.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Export(object? sender, RoutedEventArgs e)
    {
        if (_savePath is not { } path || Characters.SelectedItem is not CharacterSlot slot ||
            Weapons.SelectedItem is not WeaponChoice right || LeftWeapons.SelectedItem is not WeaponChoice left ||
            ReadAttributes() is not { } attributes)
            return;
        var armorIds = ArmorInputs.Select(input => (input.SelectedItem as ArmorChoice)?.Id ?? EquipmentCatalog.Empty).ToArray();
        var armor = new ArmorLoadout(armorIds[0], armorIds[1], armorIds[2], armorIds[3]);
        var rightAsh = SelectedAsh(RightAsh);
        var leftAsh = SelectedAsh(LeftAsh);
        var rightAffinity = SelectedAffinity(RightAffinity);
        var leftAffinity = SelectedAffinity(LeftAffinity);
        var rightLevel = SelectedLevel(RightReinforcement);
        var leftLevel = SelectedLevel(LeftReinforcement);
        var rightAmmo = (RightAmmo.SelectedItem as AmmoChoice)?.Id;
        var leftAmmo = (LeftAmmo.SelectedItem as AmmoChoice)?.Id;
        var shade = (ShadeBox.SelectedItem as ShadeChoice)?.Shade ?? CompanionShade.None;
        var preset = SelectedPreset.Preset;
        var spellIds = SpellLoadout(right.Id).ToArray();
        var (talismanIds, consumables, validQuantities) = ReadExtras();
        if (!validQuantities)
            return;
        if (_regulation is not { } regulation)
        {
            Status.Text = "Open Tool paths and locate the stock Elden Ring regulation.bin.";
            return;
        }
        if (Hp.Value is not { } hp || Damage.Value is not { } damage ||
            hp != decimal.Truncate(hp) || damage != decimal.Truncate(damage))
        {
            Status.Text = $"Enter whole HP and damage percentages from {SpiritProfile.MinPercent} to {SpiritProfile.MaxPercent}.";
            return;
        }

        SetBusy(true);
        Status.Text = $"Exporting {slot.Name} at level {attributes.Level}…";
        try
        {
            SpiritProfile profile;
            try
            {
                profile = await Task.Run(() => SaveAppearance.Import(path, slot));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Status.Text = "Could not reopen the selected save. Import it again or select a backup.";
                return;
            }
            _exportedProfile = await Task.Run(() => RegulationExporter.Export(profile with
            {
                WeaponId = right.Id, LeftWeaponId = left.Id, Attributes = attributes, Armor = armor,
                RightAshOfWarId = rightAsh, LeftAshOfWarId = leftAsh,
                RightAffinity = rightAffinity, LeftAffinity = leftAffinity,
                RightReinforcement = rightLevel, LeftReinforcement = leftLevel,
                RightAmmoId = rightAmmo, LeftAmmoId = leftAmmo,
                Shade = shade, CombatPreset = preset, SpellIds = spellIds,
                TalismanIds = talismanIds, Consumables = consumables,
                HpPercent = (int)hp, DamagePercent = (int)damage
            }, regulation, LocalPaths.NewExportFolder()));
            Status.Text = $"Exported {slot.Name}. Choose Launch offline when the game is closed.";
        }
        catch (Exception ex) when (ExpectedFailure(ex))
        {
            Status.Text = $"Export failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Launch(object? sender, RoutedEventArgs e)
    {
        if (_exportedProfile is not { } profile)
            return;
        if (_me3 is not { } me3)
        {
            Status.Text = "Open Tool paths and locate me3.exe before launching.";
            return;
        }
        try
        {
            var running = Process.GetProcessesByName("eldenring");
            var isRunning = running.Length > 0;
            foreach (var process in running)
                process.Dispose();
            if (isRunning)
            {
                Status.Text = "Elden Ring is already running. Fully quit the game before launching this profile.";
                return;
            }
            var start = new ProcessStartInfo(me3)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(profile)!
            };
            start.ArgumentList.Add("launch");
            start.ArgumentList.Add("--profile");
            start.ArgumentList.Add(profile);
            using var launcher = Process.Start(start) ?? throw new IOException("ME3 did not start.");
            Status.Text = "ME3 started with the offline profile and separate test save.";
        }
        catch (Exception ex) when (ExpectedFailure(ex))
        {
            Status.Text = $"Could not launch ME3: {ex.Message}";
        }
    }

    private void CharacterChanged(object? sender, SelectionChangedEventArgs e) => ResetExport();
    private void WeaponChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_ready)
            return;
        if (ReferenceEquals(sender, Weapons))
        {
            RefreshWeaponOptions(Weapons, RightAsh, RightAffinity, RightReinforcement);
            RefreshSpellPicker();
        }
        else if (ReferenceEquals(sender, LeftWeapons))
            RefreshWeaponOptions(LeftWeapons, LeftAsh, LeftAffinity, LeftReinforcement);
        ResetExport();
    }

    private void AshChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_ready)
            return;
        if (ReferenceEquals(sender, RightAsh))
            RefreshAffinities(RightWeaponId, RightAsh, RightAffinity);
        else if (ReferenceEquals(sender, LeftAsh))
            RefreshAffinities(SelectedWeapon(LeftWeapons).Id, LeftAsh, LeftAffinity);
        ResetExport();
    }
    private void UpgradeChanged(object? sender, SelectionChangedEventArgs e) => ResetExport();
    private void ShadeChanged(object? sender, SelectionChangedEventArgs e) => ResetExport();
    private void PresetChanged(object? sender, SelectionChangedEventArgs e) => ResetExport();

    private List<int> SpellLoadout(int weaponId)
    {
        if (!_spellLoadouts.TryGetValue(weaponId, out var ids))
            _spellLoadouts[weaponId] = ids = [.. SpellCatalog.StarterIds(weaponId)];
        return ids;
    }

    private void SpellSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (_ready)
            RefreshSpellPicker(updateEquipped: false);
    }

    private void SpellSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_ready)
            RefreshSpellControls(RightWeaponId);
    }

    private void AddSpell(object? sender, RoutedEventArgs e)
    {
        if (SpellPicker.SelectedItem is not SpellChoice spell)
            return;
        var ids = SpellLoadout(RightWeaponId);
        if (ids.Count >= SpellCatalog.SlotCount || ids.Contains(spell.Id))
            return;
        ids.Add(spell.Id);
        RefreshSpellPicker();
        ResetExport();
    }

    private void RemoveSpell(object? sender, RoutedEventArgs e)
    {
        if (EquippedSpells.SelectedItem is not SpellChoice spell)
            return;
        SpellLoadout(RightWeaponId).Remove(spell.Id);
        RefreshSpellPicker();
        ResetExport();
    }

    private void RestoreSpells(object? sender, RoutedEventArgs e)
    {
        _spellLoadouts[RightWeaponId] = [.. SpellCatalog.StarterIds(RightWeaponId)];
        RefreshSpellPicker();
        ResetExport();
    }

    private void RefreshSpellPicker(bool updateEquipped = true)
    {
        var weaponId = RightWeaponId;
        var choices = SpellCatalog.ForWeapon(weaponId);
        var ids = SpellLoadout(weaponId);
        var available = choices.Where(spell => !ids.Contains(spell.Id) &&
            Matches(spell.Name, spell.CastType == 0 ? SpellSearch.Text : IncantationSearch.Text)).ToArray();
        var previous = SpellPicker.SelectedItem as SpellChoice;
        SpellPicker.ItemsSource = available;
        SpellPicker.SelectedItem = available.Contains(previous) ? previous : available.FirstOrDefault();
        if (updateEquipped)
            EquippedSpells.ItemsSource = ids.Select(id => choices.First(spell => spell.Id == id)).ToArray();
        RefreshSpellControls(weaponId);
    }

    private void RefreshSpellControls(int weaponId)
    {
        var ids = SpellLoadout(weaponId);
        SpellPanel.IsVisible = SpellCatalog.ForWeapon(weaponId).Count > 0;
        var weapon = EquipmentCatalog.Weapon(weaponId);
        SpellSearch.IsVisible = weapon.Sorcery;
        IncantationSearch.IsVisible = weapon.Incantation;
        var kind = (weapon.Sorcery, weapon.Incantation) switch
        {
            (true, true) => "Sorceries and incantations",
            (false, true) => "Incantations",
            _ => "Sorceries"
        };
        SpellHeading.Text = $"{kind} · {ids.Count}/{SpellCatalog.SlotCount} equipped";
        SpellSearch.IsEnabled = SpellPicker.IsEnabled = EquippedSpells.IsEnabled = !_busy;
        IncantationSearch.IsEnabled = !_busy;
        var spell = SpellPicker.SelectedItem as SpellChoice;
        AddSpellButton.IsEnabled = !_busy && ids.Count < SpellCatalog.SlotCount && spell is not null;
        RemoveSpellButton.IsEnabled = !_busy && EquippedSpells.SelectedItem is SpellChoice;
        RestoreSpellsButton.IsEnabled = !_busy && !ids.SequenceEqual(SpellCatalog.StarterIds(weaponId));
        SpellRequirements.Text = spell is not null ? $"Requires {spell.Requirements.Describe()}." : "No matching unequipped spells.";
    }

    private void ApplyStarterGear(object? sender, RoutedEventArgs e)
    {
        var preset = SelectedPreset;
        if (preset.StarterRight is not { } right)
            return;
        WeaponSearch.Text = "";
        ApplyEquipmentSearch();
        Weapons.SelectedItem = EquipmentCatalog.Weapon(right);
        LeftWeapons.SelectedItem = EquipmentCatalog.Weapon(preset.StarterLeft);
        RightAsh.SelectedItem = LeftAsh.SelectedItem = AshOfWarCatalog.Default;
        RightAffinity.SelectedIndex = LeftAffinity.SelectedIndex = 0;
        RightReinforcement.SelectedIndex = LeftReinforcement.SelectedIndex = 0;
        ResetExport();
    }

    private static int? SelectedAsh(ComboBox box) =>
        box.SelectedItem is AshOfWarChoice { GemId: >= 0 } choice ? choice.GemId : null;

    private static WeaponChoice SelectedWeapon(ComboBox box) =>
        box.SelectedItem as WeaponChoice ?? EquipmentCatalog.Weapon(EquipmentCatalog.Empty);

    private static WeaponAffinity SelectedAffinity(ComboBox box) =>
        (box.SelectedItem as AffinityChoice)?.Affinity ?? WeaponAffinity.Standard;

    private static int SelectedLevel(ComboBox box) =>
        (box.SelectedItem as ReinforcementChoice)?.Level ?? 0;

    private static void RefreshAffinities(int weaponId, ComboBox ashBox, ComboBox affinityBox)
    {
        var previous = SelectedAffinity(affinityBox);
        var choices = WeaponUpgradeCatalog.ForWeapon(weaponId, SelectedAsh(ashBox));
        affinityBox.ItemsSource = choices;
        affinityBox.SelectedItem = choices.FirstOrDefault(choice => choice.Affinity == previous) ?? choices[0];
    }

    private void RefreshWeaponOptions(ComboBox weaponBox, ComboBox ashBox, ComboBox affinityBox, ComboBox reinforcementBox)
    {
        var weapon = SelectedWeapon(weaponBox);
        var previousAsh = SelectedAsh(ashBox);
        var previousLevel = SelectedLevel(reinforcementBox);
        // Suppress intermediate selection events while replacing the dropdown contents.
        var wasReady = _ready;
        _ready = false;
        try
        {
            var ashes = AshOfWarCatalog.ForWeapon(weapon.Id)
                .Where(ash => ash.GemId < 0 || ash.GemId == previousAsh || Matches(ash.Name, AshSearch.Text)).ToArray();
            ashBox.ItemsSource = ashes;
            ashBox.SelectedItem = ashes.FirstOrDefault(choice => choice.GemId == previousAsh) ?? AshOfWarCatalog.Default;
            RefreshAffinities(weapon.Id, ashBox, affinityBox);
            var levels = WeaponUpgradeCatalog.Reinforcements(weapon);
            reinforcementBox.ItemsSource = levels;
            reinforcementBox.SelectedIndex = previousLevel < levels.Count ? previousLevel : 0;
            RefreshAmmo(weapon.Id, ReferenceEquals(weaponBox, Weapons) ? RightAmmo : LeftAmmo);
        }
        finally
        {
            _ready = wasReady;
        }
    }

    private void RefreshAmmo(int weapon, ComboBox box)
    {
        var type = AmmunitionCatalog.TypeForWeapon(weapon);
        var previous = (box.SelectedItem as AmmoChoice)?.Type == type ? box.SelectedItem as AmmoChoice : AmmunitionCatalog.Default(type);
        box.ItemsSource = AmmunitionCatalog.All.Where(a => a.Type == type &&
            (a == previous || Matches(a.Name, AmmoSearch.Text))).ToArray();
        box.SelectedItem = previous;
        box.IsVisible = type != 0;
        AmmoPanel.IsVisible = RightAmmo.IsVisible || LeftAmmo.IsVisible;
    }
    private void TuningChanged(object? sender, NumericUpDownValueChangedEventArgs e) => ResetExport();

    private static bool Matches(string name, string? query) =>
        name.Replace('’', '\'').Contains((query ?? "").Trim().Replace('’', '\''), StringComparison.OrdinalIgnoreCase);

    private void EquipmentSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (_ready)
            ApplyEquipmentSearch();
    }

    private void ApplyEquipmentSearch()
    {
        static void Filter<T>(ComboBox box, IEnumerable<T> choices, Func<T, bool> matches) where T : class
        {
            var previous = box.SelectedItem as T;
            box.ItemsSource = choices.Where(choice => Equals(choice, previous) || matches(choice)).ToArray();
            box.SelectedItem = previous;
        }

        _ready = false;
        try
        {
            Filter(Weapons, EquipmentCatalog.Weapons, w => w.Id < 0 || Matches(w.Name, WeaponSearch.Text));
            Filter(LeftWeapons, EquipmentCatalog.Weapons.Where(w => w.Kind != WeaponKind.Catalyst),
                w => w.Id < 0 || Matches(w.Name, WeaponSearch.Text));
            var armor = ArmorInputs;
            foreach (var slot in Enum.GetValues<ArmorSlot>())
                Filter(armor[(int)slot], EquipmentCatalog.ArmorFor(slot), a => a.Id < 0 || Matches(a.Name, ArmorSearch.Text));
            foreach (var box in _talismans)
                Filter(box, ExtrasCatalog.Talismans, a => a.Id < 0 || Matches(a.Name, TalismanSearch.Text));
            foreach (var (box, _) in _consumables)
                Filter(box, ExtrasCatalog.Consumables, a => a.Id < 0 || Matches(a.Name, ItemSearch.Text));
            RefreshWeaponOptions(Weapons, RightAsh, RightAffinity, RightReinforcement);
            RefreshWeaponOptions(LeftWeapons, LeftAsh, LeftAffinity, LeftReinforcement);
        }
        finally
        {
            _ready = true;
        }
        Refresh();
    }

    private void BuildExtrasControls()
    {
        for (var slot = 0; slot < ExtrasCatalog.TalismanSlots; slot++)
        {
            var box = new ComboBox { ItemsSource = ExtrasCatalog.Talismans, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
            var id = slot < ExtrasCatalog.LegacyTalismans.Count ? ExtrasCatalog.LegacyTalismans[slot] : EquipmentCatalog.Empty;
            box.SelectedItem = ExtrasCatalog.Talismans.First(choice => choice.Id == id);
            AutomationProperties.SetName(box, $"Talisman {slot + 1}");
            box.SelectionChanged += (_, _) => ResetExport();
            _talismans.Add(box);
            TalismanSlots.Children.Add(box);
        }
        for (var slot = 0; slot < ExtrasCatalog.ConsumableSlots; slot++)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,110"), ColumnSpacing = 8 };
            var box = new ComboBox { ItemsSource = ExtrasCatalog.Consumables, SelectedIndex = 0,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
            var quantity = new NumericUpDown { Value = 1, Minimum = 1, Maximum = 1, FormatString = "0", ClipValueToMinMax = true };
            AutomationProperties.SetName(box, $"Quick item {slot + 1}");
            AutomationProperties.SetName(quantity, $"Quantity for quick item {slot + 1}");
            box.SelectionChanged += (_, _) =>
            {
                quantity.Maximum = Math.Max(1, (box.SelectedItem as ConsumableChoice)?.MaxQuantity ?? 0);
                if (quantity.Value is null || quantity.Value > quantity.Maximum)
                    quantity.Value = 1;
                ResetExport();
            };
            quantity.ValueChanged += TuningChanged;
            _consumables.Add((box, quantity));
            row.Children.Add(box);
            Grid.SetColumn(quantity, 1);
            row.Children.Add(quantity);
            ConsumableSlots.Children.Add(row);
        }
    }

    private (int[] Talismans, ConsumableStack[] Items, bool ValidQuantities) ReadExtras()
    {
        var talismans = _talismans.Select(box => (box.SelectedItem as TalismanChoice)?.Id ?? EquipmentCatalog.Empty).ToArray();
        var items = new List<ConsumableStack>();
        foreach (var (box, quantity) in _consumables)
        {
            if (box.SelectedItem is not ConsumableChoice { Id: >= 0 } item)
                continue;
            if (quantity.Value is not { } value || value != decimal.Truncate(value) || value < 1 || value > item.MaxQuantity)
                return (talismans, [], false);
            items.Add(new(item.Id, (int)value));
        }
        return (talismans, items.ToArray(), true);
    }

    private void ResetExport()
    {
        if (!_ready)
            return;
        var hadExport = _exportedProfile is not null;
        _exportedProfile = null;
        if (hadExport && !_busy)
            Status.Text = "Selection changed. Export again to launch the new setup.";
        Refresh();
    }

    private async void PickGame(object? sender, RoutedEventArgs e) =>
        await PickTool("Select the stock regulation.bin from the Elden Ring Game folder", "regulation.bin", path => _regulation = path);
    private async void PickMe3(object? sender, RoutedEventArgs e) =>
        await PickTool("Select Mod Engine 3", "me3.exe", path => _me3 = path);

    private async Task PickTool(string title, string fileName, Action<string> select)
    {
        SetBusy(true);
        try
        {
            var path = await Pick(title, fileName, [fileName]);
            if (path is null)
                return;
            if (!string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase))
            {
                Status.Text = $"Select {fileName}.";
                return;
            }
            select(path);
            Status.Text = $"Selected {fileName}.";
        }
        catch (Exception ex) when (ExpectedFailure(ex))
        {
            Status.Text = $"Could not select {fileName}: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<string?> Pick(string title, string filter, IReadOnlyList<string> patterns,
        IStorageFolder? start = null)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start,
            FileTypeFilter = [new FilePickerFileType(filter) { Patterns = patterns }]
        });
        if (files.Count == 0)
            return null;
        using var item = files[0];
        return item.TryGetLocalPath() ?? throw new IOException("Select a local file.");
    }

    private void SetBusy(bool value)
    {
        _busy = value;
        Refresh();
    }

    private void Refresh()
    {
        Busy.IsVisible = _busy;
        ImportButton.IsEnabled = Weapons.IsEnabled = LeftWeapons.IsEnabled = Hp.IsEnabled = Damage.IsEnabled = ShadeBox.IsEnabled =
            GameButton.IsEnabled = Me3Button.IsEnabled = !_busy;
        PresetBox.IsEnabled = !_busy;
        var preset = SelectedPreset;
        StarterGearButton.IsEnabled = !_busy && preset.StarterRight is not null;
        PresetDescription.Text = preset.Description;
        foreach (var stat in StatInputs)
            stat.IsEnabled = !_busy;
        foreach (var piece in ArmorInputs)
            piece.IsEnabled = !_busy;
        foreach (var talisman in _talismans)
            talisman.IsEnabled = !_busy;
        foreach (var (item, quantity) in _consumables)
        {
            item.IsEnabled = !_busy;
            quantity.IsEnabled = !_busy && item.SelectedItem is ConsumableChoice { Id: >= 0 };
        }
        var (talismanIds, consumables, validQuantities) = ReadExtras();
        var extrasProblem = validQuantities ? ExtrasCatalog.LoadoutProblem(talismanIds, consumables) : "Enter a whole quantity for each selected item.";
        ExtrasProblem.IsVisible = extrasProblem is not null;
        ExtrasProblem.Text = extrasProblem;
        Characters.IsEnabled = !_busy && _savePath is not null && Characters.ItemCount > 0;
        var stats = ReadAttributes();
        LevelLabel.Text = stats is null ? "Level —" : $"Level {stats.Level}";
        var right = Weapons.SelectedItem as WeaponChoice;
        var left = LeftWeapons.SelectedItem as WeaponChoice;
        RightAsh.IsEnabled = !_busy && RightAsh.ItemCount > 1;
        LeftAsh.IsEnabled = !_busy && LeftAsh.ItemCount > 1;
        RightAffinity.IsEnabled = !_busy && RightAffinity.ItemCount > 1;
        LeftAffinity.IsEnabled = !_busy && LeftAffinity.ItemCount > 1;
        RightReinforcement.IsEnabled = !_busy && RightReinforcement.ItemCount > 1;
        LeftReinforcement.IsEnabled = !_busy && LeftReinforcement.ItemCount > 1;
        AmmoSearch.IsEnabled = RightAmmo.IsEnabled = LeftAmmo.IsEnabled = !_busy;
        var rightMissing = stats is null ? "" : right?.Requirements.Describe(stats) ?? "";
        var leftMissing = stats is null ? "" : left?.Requirements.Describe(stats) ?? "";
        var problem = right is null || left is null ? null : CombatPresetChoice.LoadoutProblem(preset.Preset, right, left);
        var spellProblem = SpellCatalog.LoadoutProblem(preset.Preset, RightWeaponId, SpellLoadout(RightWeaponId), stats);
        var meetsRequirements = rightMissing.Length == 0 && leftMissing.Length == 0 && problem is null &&
            spellProblem is null && extrasProblem is null;
        RequirementsLabel.Text = stats is null ? "Enter whole attributes from 10 to 99." :
            string.Join("\n", new[] { problem, RequirementLine("Right", right, rightMissing), RequirementLine("Left", left, leftMissing) }
                .Where(line => line is not null));
        RefreshSpellControls(RightWeaponId);
        SpellProblem.IsVisible = spellProblem is not null;
        SpellProblem.Text = spellProblem;
        ExportButton.IsEnabled = !_busy && Characters.SelectedItem is CharacterSlot && right is not null &&
            left is not null && stats is not null && meetsRequirements;
        LaunchButton.IsEnabled = !_busy && _exportedProfile is not null;
        GameLabel.Text = _regulation is null ? "Game file: not found" : "Game file: regulation.bin selected";
        Me3Label.Text = _me3 is null ? "ME3: not found" : "ME3: selected";
    }

    private NpcAttributes? ReadAttributes()
    {
        var v = StatInputs.Select(input => input.Value).ToArray();
        if (v.Any(x => x is null || x.Value != decimal.Truncate(x.Value) ||
            x.Value is < NpcAttributes.Minimum or > NpcAttributes.Maximum))
            return null;
        return new((int)v[0]!.Value, (int)v[1]!.Value, (int)v[2]!.Value, (int)v[3]!.Value,
            (int)v[4]!.Value, (int)v[5]!.Value, (int)v[6]!.Value, (int)v[7]!.Value);
    }

    private static string RequirementLine(string hand, WeaponChoice? weapon, string missing)
    {
        if (weapon is null || weapon.Id == EquipmentCatalog.Empty)
            return $"{hand}: empty.";
        var requirements = weapon.Requirements.Describe();
        var validation = missing.Length > 0 ? $"Raise {missing} to export." :
            weapon.Tested ? "Requirements met." : "Requirements met. AI use untested.";
        return $"{hand} · {requirements}: {validation}";
    }

    private static bool ExpectedFailure(Exception ex) =>
        ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or System.ComponentModel.Win32Exception or System.Xml.XmlException or
            System.Runtime.InteropServices.COMException or TimeoutException;
}
