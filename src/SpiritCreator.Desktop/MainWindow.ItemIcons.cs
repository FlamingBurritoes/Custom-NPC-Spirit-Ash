using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SpiritCreator.Desktop;

public sealed partial class MainWindow
{
    private readonly List<ItemIconBinding> _itemIconBindings = [];
    private InventoryIcons? _itemIcons;
    private CancellationTokenSource? _itemIconLoad;
    private bool _itemIconsClosed;
    private int _iconSize = 128;

    private sealed class ItemIconBinding(SelectingItemsControl picker, Image image)
    {
        internal SelectingItemsControl Picker { get; } = picker;
        internal Image Image { get; } = image;
        internal InventoryItem? Current { get; set; }
    }

    private void RegisterItemIcons()
    {
        BindItemIcon(TargetAshBox, TargetAshIcon);
        BindItemIcon(Weapons, RightWeaponIcon);
        BindItemIcon(LeftWeapons, LeftWeaponIcon);
        BindItemIcon(RightAsh, RightAshIcon);
        BindItemIcon(LeftAsh, LeftAshIcon);
        BindItemIcon(HeadArmor, HeadIcon);
        BindItemIcon(ChestArmor, ChestIcon);
        BindItemIcon(HandsArmor, HandsIcon);
        BindItemIcon(LegsArmor, LegsIcon);
        BindItemIcon(RightAmmo, RightAmmoIcon);
        BindItemIcon(LeftAmmo, LeftAmmoIcon);
        BindItemIcon(SpellPicker, SpellIcon);
        BindItemIcon(EquippedSpells, EquippedSpellIcon);
        SizeChanged += (_, e) => ResizeItemIcons(e.NewSize.Width);
    }

    private Image CreateItemIcon(SelectingItemsControl picker)
    {
        var image = new Image();
        BindItemIcon(picker, image);
        return image;
    }

    private void BindItemIcon(SelectingItemsControl picker, Image image)
    {
        image.Width = image.Height = _iconSize;
        image.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        if (picker is ComboBox)
            picker.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        image.Stretch = Stretch.Uniform;
        image.IsHitTestVisible = false;
        image.IsVisible = false;
        _itemIconBindings.Add(new(picker, image));
    }

    private void ResizeItemIcons(double width)
    {
        var size = width switch { >= 1000 => InventoryIcons.IconSize, >= 720 => 128, _ => 104 };
        if (_iconSize == size)
            return;
        _iconSize = size;
        foreach (var binding in _itemIconBindings)
            binding.Image.Width = binding.Image.Height = size;
    }

    private async void LoadItemIcons()
    {
        _itemIconLoad?.Cancel();
        _itemIcons = null;
        ClearItemIcons();
        IconStatus.IsVisible = true;
        if (_regulation is not { } regulation)
        {
            IconStatus.Text = "To show item images, select the installed game’s regulation.bin under Tool paths.";
            return;
        }

        var pending = new CancellationTokenSource();
        _itemIconLoad = pending;
        var token = pending.Token;
        IconStatus.Text = "Loading inventory images from Elden Ring…";
        try
        {
            var icons = await Task.Run(() => InventoryIcons.Load(regulation, token), token);
            if (token.IsCancellationRequested)
                return;
            _itemIcons = icons;
            RefreshItemIcons();
            IconStatus.IsVisible = false;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) when (ExpectedFailure(ex) || ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            if (!token.IsCancellationRequested)
                IconStatus.Text = "Item images unavailable. Select the installed Steam 1.17.3 Game folder’s regulation.bin under Tool paths.";
        }
        finally
        {
            if (ReferenceEquals(_itemIconLoad, pending))
                _itemIconLoad = null;
            pending.Dispose();
        }
    }

    private void RefreshItemIcons()
    {
        if (_itemIcons is not { } icons || _itemIconsClosed)
            return;
        foreach (var binding in _itemIconBindings)
        {
            var item = ItemFor(binding.Picker.SelectedItem);
            if (binding.Current == item)
                continue;
            var rgba = item is { Id: >= 0 } key ? icons.ReadRgba(key) : null;
            SetIcon(binding.Image, rgba is null ? null : MakeIcon(rgba));
            binding.Current = item;
        }
    }

    private static InventoryItem? ItemFor(object? item) => item switch
    {
        WeaponChoice choice => new(InventoryKind.Weapon, choice.Id),
        AmmoChoice choice => new(InventoryKind.Weapon, choice.Id),
        ArmorChoice choice => new(InventoryKind.Armor, choice.Id),
        TalismanChoice choice => new(InventoryKind.Talisman, choice.Id),
        ConsumableChoice choice => new(InventoryKind.Consumable, choice.Id),
        SpellChoice choice => new(InventoryKind.Spell, choice.Id),
        AshOfWarChoice choice => new(InventoryKind.AshOfWar, choice.GemId),
        AshTargetChoice choice => new(InventoryKind.SpiritAsh, choice.GoodsId),
        _ => null
    };

    private static WriteableBitmap MakeIcon(byte[] pixels)
    {
        const int size = InventoryIcons.IconSize;
        // Convert straight RGBA to premultiplied BGRA for Avalonia/Skia.
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var a = pixels[i + 3];
            var r = pixels[i];
            pixels[i] = (byte)((pixels[i + 2] * a + 127) / 255);
            pixels[i + 1] = (byte)((pixels[i + 1] * a + 127) / 255);
            pixels[i + 2] = (byte)((r * a + 127) / 255);
        }
        var bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var access = bitmap.Lock();
        for (var row = 0; row < size; row++)
            Marshal.Copy(pixels, row * size * 4, access.Address + row * access.RowBytes, size * 4);
        return bitmap;
    }

    private static void SetIcon(Image image, Bitmap? source)
    {
        var previous = image.Source as Bitmap;
        image.Source = source;
        image.IsVisible = source is not null;
        previous?.Dispose();
    }

    private void ClearItemIcons()
    {
        foreach (var binding in _itemIconBindings)
        {
            SetIcon(binding.Image, null);
            binding.Current = null;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _itemIconsClosed = true;
        _itemIconLoad?.Cancel();
        _itemIcons = null;
        ClearItemIcons();
        base.OnClosed(e);
    }
}
