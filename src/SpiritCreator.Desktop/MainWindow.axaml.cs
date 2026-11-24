using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SpiritCreator.Desktop;

public sealed partial class MainWindow : Window
{
    private string? _savePath;
    private string? _exportedProfile;
    private string? _regulation;
    private string? _witchy;
    private string? _me3;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        Weapons.ItemsSource = SpiritProfile.SupportedWeapons;
        Weapons.SelectedIndex = 0;
        _regulation = LocalPaths.FindStockRegulation();
        _witchy = LocalPaths.FindWitchy();
        _me3 = LocalPaths.FindMe3();
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
            Weapons.SelectedItem is not WeaponChoice weapon)
            return;
        if (_regulation is not { } regulation || _witchy is not { } witchy)
        {
            Status.Text = "Open Tool paths and locate the stock regulation.bin and WitchyBND 3.0.1.0.";
            return;
        }

        SetBusy(true);
        Status.Text = $"Exporting {slot.Name} with two {weapon.Name}s. This can take a minute…";
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
            _exportedProfile = await Task.Run(() => RegulationExporter.Export(profile with { WeaponId = weapon.Id },
                regulation, witchy, LocalPaths.NewExportFolder()));
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
    private void WeaponChanged(object? sender, SelectionChangedEventArgs e) => ResetExport();

    private void ResetExport()
    {
        var hadExport = _exportedProfile is not null;
        _exportedProfile = null;
        if (hadExport && !_busy)
            Status.Text = "Selection changed. Export again to launch the new setup.";
        Refresh();
    }

    private async void PickGame(object? sender, RoutedEventArgs e) =>
        await PickTool("Select the stock regulation.bin from the Elden Ring Game folder", "regulation.bin", path => _regulation = path);
    private async void PickWitchy(object? sender, RoutedEventArgs e) =>
        await PickTool("Select WitchyBND 3.0.1.0", "WitchyBND.exe", path => _witchy = path);
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
        ImportButton.IsEnabled = Weapons.IsEnabled = GameButton.IsEnabled = WitchyButton.IsEnabled = Me3Button.IsEnabled = !_busy;
        Characters.IsEnabled = !_busy && _savePath is not null && Characters.ItemCount > 0;
        ExportButton.IsEnabled = !_busy && Characters.SelectedItem is CharacterSlot && Weapons.SelectedItem is WeaponChoice;
        LaunchButton.IsEnabled = !_busy && _exportedProfile is not null;
        GameLabel.Text = _regulation is null ? "Game file: not found" : "Game file: regulation.bin selected";
        WitchyLabel.Text = _witchy is null ? "WitchyBND: not found" : "WitchyBND: selected";
        Me3Label.Text = _me3 is null ? "ME3: not found" : "ME3: selected";
    }

    private static bool ExpectedFailure(Exception ex) =>
        ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or System.ComponentModel.Win32Exception or System.Xml.XmlException or
            System.Runtime.InteropServices.COMException or TimeoutException;
}
