using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ImageForge.Core;
using Microsoft.Win32;

namespace ImageForge.App;

public partial class MainWindow
{
    private readonly ObservableCollection<ImageRow> _images = [];
    private readonly ObservableCollection<PresetEntry> _presets = [];
    private sealed class ImageRow(string path, ImageEdition edition, long sizeBytes) : INotifyPropertyChanged
    {
        public string Path { get; } = path;
        public ImageEdition Edition { get; } = edition;
        public long SizeBytes { get; } = sizeBytes;
        private bool mounted;
        public bool Mounted
        {
            get => mounted;
            set { mounted = value; RefreshStatus(); }
        }
        public void RefreshStatus() => PropertyChanged?.Invoke(this, new(nameof(Status)));
        public bool IsActive { get; private set; }
        public bool Indeterminate { get; private set; }
        public double ProgressValue { get; private set; }
        public string ProgressLabel => Indeterminate ? "Working…" : $"{ProgressValue:0.#}%";
        public event PropertyChangedEventHandler? PropertyChanged;
        public void SetActivity(double? percentage)
        {
            IsActive = true; Indeterminate = percentage is null; ProgressValue = percentage ?? 0;
            foreach (var name in new[] { nameof(IsActive), nameof(Indeterminate), nameof(ProgressValue), nameof(ProgressLabel) }) PropertyChanged?.Invoke(this, new(name));
        }
        public void ClearActivity() { IsActive = false; PropertyChanged?.Invoke(this, new(nameof(IsActive))); }
        public string Category => Format is "ESD" or "SWM" ? "Read-only" : "Other";
        public bool CompactDates { get; set; } = true;
        public string Description => Edition.Description;
        public string Modified => File.Exists(Path) ? File.GetLastWriteTime(Path).ToString(CompactDates ? "dd MMM HH:mm" : "yyyy-MM-dd HH:mm:ss") : "—";
        public void RefreshPresentation() => PropertyChanged?.Invoke(this, new(nameof(Modified)));
        public string Name => $"{Edition.Index} · {Edition.Name}";
        public int Index => Edition.Index;
        public string Format => System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant();
        public string SizeMb => (SizeBytes / 1024d / 1024d).ToString("N0");
        public string Status => !File.Exists(Path) ? "Missing" : Mounted ? "Mounted" : Format is "ESD" or "SWM" ? "Load to edit" : "Available";
    }
    private sealed class PresetEntry(string path) : INotifyPropertyChanged
    {
        public string Path { get; } = path;
        public bool CompactDates { get; set; } = true;
        public GridLength DateColumnWidth => new(CompactDates ? 95 : 145);
        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
        public string Modified => File.Exists(Path) ? File.GetLastWriteTime(Path).ToString(CompactDates ? "dd MMM HH:mm" : "yyyy-MM-dd HH:mm:ss") : "File missing";
        public event PropertyChangedEventHandler? PropertyChanged;
        public void RefreshPresentation() { PropertyChanged?.Invoke(this, new(nameof(Modified))); PropertyChanged?.Invoke(this, new(nameof(DateColumnWidth))); }
    }
    private sealed record WorkspacePreferences(string? Dism, string? Oscdimg, List<string>? Presets);
    private static string PreferencesPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImageForge", "workspace.json");

    private void InitializeWorkspace()
    {
        ImageList.ItemsSource = _images;
        PresetList.ItemsSource = _presets;
        var view = CollectionViewSource.GetDefaultView(_images);
        view.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
        view.GroupDescriptions.Add(new PropertyGroupDescription("Path"));
        view.Filter = value => value is ImageRow row && (SourceKindFilter.SelectedIndex == 0 || row.Category == ((SourceKindFilter.SelectedItem as ComboBoxItem)?.Content as string)) && (string.IsNullOrWhiteSpace(ImageFilterBox.Text) ||
            (row.Name + " " + row.Path + " " + row.Format).Contains(ImageFilterBox.Text, StringComparison.OrdinalIgnoreCase));
        OscdimgPathBox.Text = ImageTools.FindOscdimg() ?? "";
        if (File.Exists(PreferencesPath))
        {
            try
            {
                var prefs = JsonSerializer.Deserialize<WorkspacePreferences>(File.ReadAllText(PreferencesPath));
                DismPathBox.Text = prefs?.Dism ?? "";
                OscdimgPathBox.Text = prefs?.Oscdimg ?? OscdimgPathBox.Text;
                foreach (var path in prefs?.Presets?.Take(100) ?? [])
                    if (!string.IsNullOrWhiteSpace(path) && !_presets.Any(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                        _presets.Add(new(path));
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            { Log("Workspace preferences could not be read: " + ex.Message); }
        }
        InitializeDesignQueues();
        Navigate(0);
    }

    private void SavePreferences()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
            var temp = PreferencesPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(new WorkspacePreferences(DismPathBox.Text.Trim(), OscdimgPathBox.Text.Trim(), _presets.Select(p => p.Path).Take(100).ToList()), JsonOptions));
            File.Move(temp, PreferencesPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log("Workspace preferences were not saved: " + ex.Message); }
    }

    private void RememberPreset(string path)
    {
        path = Path.GetFullPath(path);
        foreach (var previous in _presets.Where(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase)).ToArray()) _presets.Remove(previous);
        _presets.Insert(0, new(path) { CompactDates = _toolSettings.CompactDates });
        while (_presets.Count > 100) _presets.RemoveAt(_presets.Count - 1);
        SavePreferences();
    }

    private void Navigation_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_navigating && WorkspaceTabs is not null && NavigationList.SelectedItem is ListBoxItem { Tag: string tag } && int.TryParse(tag, out var index))
            Navigate(index);
    }
    private void ShowTools_Click(object sender, RoutedEventArgs e) => Navigate(5);
    private void ShowCompatibility_Click(object sender, RoutedEventArgs e) => Navigate(7);
    private void OpenGitHub_Click(object sender, RoutedEventArgs e) => OpenLink("https://github.com/sethcroft/ImageForge");
    private void OpenDocumentation_Click(object sender, RoutedEventArgs e) => OpenLink("https://imageforge-preview-docs.loftydeer260.chatgpt.site/#compatibility");
    private void OpenLink(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not open browser"); }
    }
    private void ImageFilter_Changed(object sender, TextChangedEventArgs e)
    {
        if (ImageList?.ItemsSource is not null) CollectionViewSource.GetDefaultView(_images).Refresh();
    }
    private void ImageSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_activeMount is not null || ImageList.SelectedItem is not ImageRow row) return;
        if (!SwitchToImageProject()) { ImageList.SelectedItem = null; return; }
        ImagePathBox.Text = row.Path;
        _editionsSource = row.Path;
        var editions = _images.Where(r => r.Path.Equals(row.Path, StringComparison.OrdinalIgnoreCase)).Select(r => r.Edition).ToArray();
        EditionBox.ItemsSource = editions;
        EditionBox.SelectedItem = row.Edition;
        UpdateMountBadge();
    }

    private async Task AddSourceAsync(string path)
    {
        if (!SwitchToImageProject()) return;
        _activitySource = path;
        path = Path.GetFullPath(path);
        var editions = await _dism.GetEditionsAsync(path);
        if (editions.Count == 0) throw new InvalidDataException("DISM returned no readable editions for " + path);
        AddSourceRows(path, editions);
        ImageList.SelectedItem = _images.First(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        ImagePathBox.Text = path;
        EditionBox.ItemsSource = editions;
        EditionBox.SelectedIndex = 0;
        _editionsSource = path;
        Log($"Read {editions.Count} edition(s) from {path}.");
    }
    private void AddSourceRows(string path, IReadOnlyList<ImageEdition> editions)
    {
        path = Path.GetFullPath(path);
        foreach (var existing in _images.Where(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase)).ToArray()) _images.Remove(existing);
        var size = new FileInfo(path).Length;
        foreach (var edition in editions) _images.Add(new(path, edition, size) { CompactDates = _toolSettings.CompactDates });
        SideImageCount.Text = _images.Count.ToString();
        ImageCountBadge.Text = $"{_images.Count} edition{(_images.Count == 1 ? "" : "s")}";
        ImageEmptyState.Visibility = _images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void AddImage_Click(object sender, RoutedEventArgs e)
    {
        var picker = FilePicker("Windows image files|*.wim;*.esd;*.swm;*.vhd;*.vhdx", true);
        if (picker.ShowDialog(this) != true) { _operationCancelled = true; return; }
        await Run("Read image sources", async () =>
        {
            RequireUnmounted();
            foreach (var path in picker.FileNames) await AddSourceAsync(path);
            Navigate(0);
        });
    }
    private async void AddDirectory_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Scan a Windows image or installation-media folder" };
        if (picker.ShowDialog(this) != true) { _operationCancelled = true; return; }
        await Run("Scan image folder", async () =>
        {
            RequireUnmounted();
            await ScanFolderAsync(picker.FolderName);
            Navigate(0);
        });
    }
    private async Task ScanFolderAsync(string directory)
    {
        var result = await Task.Run(() => ImageTools.DiscoverImages(directory));
        Log($"Found {result.Paths.Count} WIM/ESD file(s); skipped {result.SkippedDirectories} inaccessible or linked folder(s)." +
            (result.LimitReached ? " Scan limit reached (four nested levels, 1,000 folders or 256 image files); add deeper sources directly." : ""));
        if (result.Paths.Count == 0) throw new InvalidOperationException("No WIM or ESD files were found. For an ISO, use Import ISO first.");
        foreach (var path in result.Paths) await AddSourceAsync(path);
    }
    private async void ForgetSource_Click(object sender, RoutedEventArgs e) => await Run("Forget image source", () =>
    {
        RequireUnmounted();
        var path = Path.GetFullPath(ImagePathBox.Text.Trim());
        foreach (var row in _images.Where(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase)).ToArray()) _images.Remove(row);
        EditionBox.ItemsSource = null;
        ImagePathBox.Clear();
        _editionsSource = null;
        SideImageCount.Text = _images.Count.ToString();
        ImageCountBadge.Text = $"{_images.Count} editions";
        ImageEmptyState.Visibility = _images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Log("Removed source from the library. No files were deleted.");
        return Task.CompletedTask;
    });

    private async void LoadSelectedPreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetList.SelectedItem is not PresetEntry preset) { MessageBox.Show(this, "Select a saved preset, or use Import."); return; }
        await Run("Load preset", () => LoadPresetPathAsync(preset.Path));
    }

    private async void ExportEdition_Click(object sender, RoutedEventArgs e) => await Run("Export edition", async () =>
    {
        RequireUnmounted();
        var source = Path.GetFullPath(ImagePathBox.Text.Trim());
        if (!source.Equals(_editionsSource, StringComparison.OrdinalIgnoreCase) || EditionBox.SelectedItem is not ImageEdition edition)
            throw new InvalidOperationException("Select a source edition in Image, or read editions for the selected source first.");
        var picker = new SaveFileDialog { Title = "Export selected edition to a NEW file", Filter = "WIM image|*.wim|ESD image|*.esd", FileName = "install-export.wim", OverwritePrompt = true };
        if (picker.ShowDialog(this) != true) { _operationCancelled = true; return; }
        _activitySource = source;
        if (ExportAllCheck.IsChecked == true) await _dism.ExportAllEditionsAsync(source, picker.FileName);
        else await _dism.ExportEditionAsync(source, edition.Index, picker.FileName);
        await AddSourceAsync(picker.FileName);
        Log("Image export completed. The output edition indices are shown in Image.");
    });

    private async void SplitImage_Click(object sender, RoutedEventArgs e) => await Run("Split WIM image", async () =>
    {
        RequireUnmounted();
        if (!int.TryParse(SplitSizeBox.Text, out var size)) throw new ArgumentException("Enter a split size in MB, such as 3800.");
        var source = Path.GetFullPath(ImagePathBox.Text.Trim());
        var picker = new SaveFileDialog { Title = "Choose a NEW split-set filename in an empty output folder", Filter = "Split WIM|*.swm", FileName = "install.swm" };
        if (picker.ShowDialog(this) != true) { _operationCancelled = true; return; }
        await _dism.SplitImageAsync(source, picker.FileName, size);
        Log("Split complete. Keep every SWM part together. A single resource can exceed the requested part size; check the actual files before copying to FAT32.");
    });

    private async void ImportIso_Click(object sender, RoutedEventArgs e)
    {
        var iso = FilePicker("Windows ISO files|*.iso");
        if (iso.ShowDialog(this) != true) return;
        var destination = new SaveFileDialog { Title = "Choose a parent folder and enter a NEW media-folder name", Filter = "New media folder|*", FileName = "Windows-media", AddExtension = false, CheckPathExists = true };
        if (destination.ShowDialog(this) != true) return;
        await Run("Import ISO media", async () =>
        {
            RequireUnmounted();
            await ImageTools.ImportIsoAsync(iso.FileName, destination.FileName, Log);
            MediaFolderBox.Text = destination.FileName;
            await ScanFolderAsync(destination.FileName);
            Navigate(0);
        });
    }
    private async void CreateIso_Click(object sender, RoutedEventArgs e) => await Run("Create Windows ISO", async () =>
    {
        RequireUnmounted();
        var media = DismCommands.SafePath(MediaFolderBox.Text.Trim());
        // Block ISO creation if any WIM/ESD in the media tree is currently mounted,
        // including mounts created by another application.
        if (await _dism.HasMountedSourceUnderAsync(media))
            throw new InvalidOperationException("A source image under this media folder is still mounted. Save/discard every mount before creating the ISO.");
        var picker = new SaveFileDialog { Title = "Create a NEW BIOS + UEFI Windows ISO", Filter = "ISO image|*.iso", FileName = "ImageForge.iso" };
        if (picker.ShowDialog(this) != true) { _operationCancelled = true; return; }
        await ImageTools.CreateIsoAsync(OscdimgPathBox.Text.Trim(), media, picker.FileName, IsoLabelBox.Text.Trim(), Log);
        Log("ISO created. Test both installation and boot behavior in a disposable VM before deployment.");
    });
    private void BrowseMedia_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Select complete extracted Windows installation media" };
        if (picker.ShowDialog(this) == true) MediaFolderBox.Text = picker.FolderName;
    }
    private void BrowseOscdimg_Click(object sender, RoutedEventArgs e)
    {
        var picker = FilePicker("Microsoft ADK ISO tool|oscdimg.exe");
        if (picker.ShowDialog(this) == true) { OscdimgPathBox.Text = picker.FileName; SavePreferences(); }
    }
    private void BrowseDism_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMount is not null) { MessageBox.Show(this, "Unmount the current image before switching the servicing engine."); return; }
        var picker = FilePicker("Microsoft DISM tool|dism.exe");
        if (picker.ShowDialog(this) == true) { DismPathBox.Text = picker.FileName; SavePreferences(); }
    }
}
