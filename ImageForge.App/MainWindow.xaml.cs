using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using ImageForge.Core;
using Microsoft.Win32;

namespace ImageForge.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<PlanStep> _steps = [];
    private readonly DismService _dism;
    private readonly DiagnosticLog _diagnostics = new();
    private ServicingTarget? _installation;
    private string _operationStage = "Ready";
    private string? _activitySource;
    private int? _activityIndex;
    private CancellationTokenSource? _downloadCancellation;
    private bool _busy;
    private bool _operationCancelled;
    private string? _activeMount;
    private string? _editionsSource;
    private string? _activeImage;
    private int _activeIndex;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public MainWindow()
    {
        InitializeComponent();
        _dism = new DismService(Log);
        _dism.PhaseChanged += (stage, index) => Dispatcher.Invoke(() => { _activityIndex = index; ReportActivity(stage, null); });
        Closed += (_, _) => _diagnostics.Dispose();
        InitializeWorkspace();
        PlanList.ItemsSource = _steps;
        _steps.CollectionChanged += (_, _) => QueueBadge.Text = $"{_steps.Count} change{(_steps.Count == 1 ? "" : "s")} queued";
        MountPathBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImageForge", "Mount");
        Log("ImageForge 0.6 — image and deployed Windows workspace.");
        UpdateMountBadge();
        LoadToolSettings();
    }

    private void Log(string text)
    {
        _diagnostics.Write(text);
        if (text.StartsWith("DISM reports a reboot is required", StringComparison.Ordinal)) _restartRequired = true;
        if (OperationProgress.Percentage(text) is { } percentage) Dispatcher.Invoke(() => ReportActivity(_operationStage, percentage));
        else if (text.StartsWith("Copying ", StringComparison.Ordinal)) Dispatcher.Invoke(() => ReportActivity("Copying Windows media", null));
    }

    private void ReportActivity(string stage, double? percentage)
    {
        if (!_busy) return;
        _operationStage = stage;
        StatusText.Text = stage + (percentage is { } value ? $" · {value:0.#}%" : "…");
        BusyProgress.IsIndeterminate = percentage is null;
        BusyProgress.Value = percentage ?? 0;
        foreach (var row in _images)
            if (row.Path.Equals(_activitySource, StringComparison.OrdinalIgnoreCase) && (_activityIndex is null || row.Index == _activityIndex)) row.SetActivity(percentage);
            else row.ClearActivity();
    }

    private ServicingTarget EffectiveTarget => _installation ?? ServicingTarget.Mounted(RequireMount());
    private void OpenDiagnostics_Click(object sender, RoutedEventArgs e) => OpenLink(_diagnostics.DirectoryPath);

    private async Task Run(string title, Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        _operationCancelled = false;
        _activitySource = _installation is null ? _activeImage ?? _editionsSource : null;
        _activityIndex = _activeIndex > 0 ? _activeIndex : (EditionBox.SelectedItem as ImageEdition)?.Index;
        WorkspaceBody.IsEnabled = false;
        CommandRibbon.IsEnabled = false;
        TopMenu.IsEnabled = false;
        ApplicationMenuButton.IsEnabled = false;
        BusyProgress.Visibility = Visibility.Visible;
        ReportActivity(ActivityTitle(title), null);
        try
        {
            _dism.ExecutableOverride = string.IsNullOrWhiteSpace(DismPathBox.Text) ? null : DismPathBox.Text.Trim();
            await action();
            StatusText.Text = _operationCancelled ? "Ready" : "Ready · " + title + " completed" + (_restartRequired ? " · Restart required" : "");
        }
        catch (OperationCanceledException) when (_downloadCancellation?.IsCancellationRequested == true)
        {
            Log("Download cancelled by the user.");
            StatusText.Text = "Download cancelled";
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex);
            StatusText.Text = title + " could not complete";
            var diagnosticMessage = _diagnostics.Available ? "Details are in the diagnostics file (Help → Diagnostics folder)." : "Diagnostics could not be saved. Check disk space and folder access.";
            MessageBox.Show(this, ex.Message + "\n\n" + diagnosticMessage + " A failed operation may have left partial changes or output.", "Operation stopped", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _busy = false;
            WorkspaceBody.IsEnabled = true;
            CommandRibbon.IsEnabled = true;
            TopMenu.IsEnabled = true;
            ApplicationMenuButton.IsEnabled = true;
            BusyProgress.Visibility = Visibility.Collapsed;
            CancelDownloadButton.Visibility = Visibility.Collapsed;
            foreach (var row in _images) row.ClearActivity();
            UpdateMountBadge();
        }
    }

    private void UpdateMountBadge()
    {
        MountBadge.Text = _installation?.DisplayName ?? (_activeMount is null ? "No image loaded" : "Image mounted · Offline");
        TargetCaption.Text = _installation is { } target ? target.DisplayName + " · Changes are written immediately when applied." : _activeMount is not null ? "Loaded image · Apply changes, then save or discard." : "Choose an image or deployed Windows installation.";
        DismPathBox.IsReadOnly = _activeMount is not null;
        ImagePathBox.IsReadOnly = _activeMount is not null;
        EditionBox.IsEnabled = _activeMount is null;
        SaveImageButton.IsEnabled = _activeMount is not null;
        DiscardImageButton.IsEnabled = _activeMount is not null;
        SharedSaveButton.IsEnabled = _activeMount is not null;
        SharedDiscardButton.IsEnabled = _activeMount is not null;
        MountImageButton.IsEnabled = _activeMount is null && _installation is null && _selectedLibraryNode?.Edition is not null && EditionBox.SelectedItem is not null;
        foreach (var row in _images) row.Mounted = _activeMount is not null && row.Index == _activeIndex && row.Path.Equals(_activeImage, StringComparison.OrdinalIgnoreCase);
        UpdateEditingNavigation();
    }
    private string RequireMount() => _activeMount ?? throw new InvalidOperationException("Mount an image or reconnect an ImageForge-owned mount first.");
    private static string ActivityTitle(string title) => title switch
    {
        "Mount image" => "Loading image", "Save image" => "Saving image", "Discard mount" => "Unmounting image",
        "Export edition" => "Exporting image", "Create Windows ISO" => "Saving ISO", "Import ISO media" => "Opening ISO",
        "Read image editions" or "Read image sources" => "Reading image editions", "Scan image folder" => "Scanning images",
        "Read optional features" => "Loading Windows features", "Apply changes" => "Applying changes", _ => title
    };
    private void RequireUnmounted()
    {
        if (_activeMount is not null) throw new InvalidOperationException("Save or discard the current mount before changing the image source.");
    }

    private static OpenFileDialog FilePicker(string filter, bool multi = false) => new() { Filter = filter, Multiselect = multi, CheckFileExists = true };

    private void BrowseMount_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMount is not null) { MessageBox.Show(this, "Unmount the current image before changing its mount directory."); return; }
        var picker = new OpenFolderDialog { Title = "Select an empty mount folder" };
        if (picker.ShowDialog(this) == true) MountPathBox.Text = picker.FolderName;
    }

    private async void ReadEditions_Click(object sender, RoutedEventArgs e) => await Run("Read image editions", async () =>
    {
        RequireUnmounted();
        var path = Path.GetFullPath(ImagePathBox.Text.Trim());
        await AddSourceAsync(path);
    });

    private async void Mount_Click(object sender, RoutedEventArgs e) => await Run("Mount image", async () =>
    {
        RequireUnmounted();
        var image = Path.GetFullPath(ImagePathBox.Text.Trim());
        if (!string.Equals(image, _editionsSource, StringComparison.OrdinalIgnoreCase) || EditionBox.SelectedItem is not ImageEdition edition)
            throw new InvalidOperationException("Read editions for the current source and select one before mounting.");
        var mount = Path.GetFullPath(MountPathBox.Text.Trim());
        _installation = null;
        _activitySource = image;
        if (Path.GetExtension(image).ToLowerInvariant() is ".esd" or ".swm")
        {
            var cacheName = Path.GetFileNameWithoutExtension(image);
            cacheName = cacheName[..Math.Min(48, cacheName.Length)] + "-" + Guid.NewGuid().ToString("N")[..8];
            var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImageForge", "Images", cacheName);
            Directory.CreateDirectory(cache);
            var cached = Path.Combine(cache, "install.wim");
            ReportActivity("Preparing image for editing", null);
            await _dism.ExportEditionAsync(image, edition.Index, cached);
            await AddSourceAsync(cached);
            image = cached;
            edition = (ImageEdition)EditionBox.SelectedItem;
            _activitySource = image;
        }
        ReportActivity("Loading image", null);
        _activityIndex = edition.Index;
        await _dism.MountAsync(image, edition.Index, mount);
        _activeMount = mount;
        _installation = null;
        _activeImage = image;
        _activeIndex = edition.Index;
        Log("Mounted. Queue drivers, updates, or optional feature changes, then open Apply.");
    });

    private async void Reconnect_Click(object sender, RoutedEventArgs e) => await Run("Reconnect mount", async () =>
    {
        RequireUnmounted();
        if (!SwitchToImageProject()) return;
        var mount = Path.GetFullPath(MountPathBox.Text.Trim());
        await _dism.ReconcileOwnedMountAsync(mount);
        if (!_dism.IsMountedOwned(mount)) throw new InvalidOperationException("No matching ImageForge-owned mount is available at that directory.");
        _activeMount = mount;
        // Source fields are deliberately not inferred from editable text after reconnect.
        _activeImage = null;
        _activeIndex = 0;
        Log("Reconnected to the verified owned mount.");
    });

    private void AddStep(PlanStep step)
    {
        if (!_steps.Contains(step)) _steps.Add(step);
        Log($"Queued {step.Kind}: {step.Value}");
    }

    private void AddDrivers_Click(object sender, RoutedEventArgs e)
    {
        if (_installation?.Kind == ServicingTargetKind.RunningWindows) { MessageBox.Show(this, "INF integration is offline-only. Choose an image or another Windows drive."); return; }
        var picker = FilePicker("Driver INF files|*.inf", true);
        if (picker.ShowDialog(this) == true) foreach (var file in picker.FileNames) AddStep(new("add-driver", file));
    }
    private void AddDriverFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_installation?.Kind == ServicingTargetKind.RunningWindows) { MessageBox.Show(this, "INF integration is offline-only. Choose an image or another Windows drive."); return; }
        var picker = new OpenFolderDialog { Title = "Driver folder — includes subfolders" };
        if (picker.ShowDialog(this) == true) AddStep(new("add-driver", picker.FolderName));
    }
    private void AddPackages_Click(object sender, RoutedEventArgs e)
    {
        var picker = FilePicker("Windows update packages|*.cab;*.msu", true);
        if (picker.ShowDialog(this) == true) foreach (var file in picker.FileNames) AddStep(new("add-package", file));
    }

    private async void RefreshFeatures_Click(object sender, RoutedEventArgs e) => await Run("Read optional features", async () =>
        FeatureList.ItemsSource = await _dism.GetFeaturesAsync(EffectiveTarget));
    private void EnableFeatures_Click(object sender, RoutedEventArgs e) => QueueFeatures("enable-feature");
    private void DisableFeatures_Click(object sender, RoutedEventArgs e) => QueueFeatures("disable-feature");
    private void QueueFeatures(string kind)
    {
        foreach (var feature in FeatureList.SelectedItems.Cast<FeatureInfo>().ToArray())
        {
            foreach (var old in _steps.Where(s => s.Value == feature.Name && s.Kind is "enable-feature" or "disable-feature").ToArray()) _steps.Remove(old);
            AddStep(new(kind, feature.Name));
        }
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_steps.Count == 0) { MessageBox.Show(this, "Queue a driver, update, or feature change first."); return; }
        ServicingTarget target;
        try { target = EffectiveTarget; } catch (InvalidOperationException ex) { MessageBox.Show(this, ex.Message); return; }
        var effect = target.WritesImmediately ? "Changes are written immediately to this Windows installation. There is no discard operation; a restart may be required." : "Changes remain in the mount until you save or discard.";
        if (MessageBox.Show(this, $"Apply {_steps.Count} queued change(s) to:\n{target.DisplayName}\n\n{effect}", "Apply changes", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await Run("Apply changes", async () =>
        {
            foreach (var step in _steps) DismCommands.ForTarget(target, step);
            foreach (var step in _steps.ToArray())
            {
                await _dism.ApplyStepAsync(target, step);
                _steps.Remove(step);
            }
            FeatureList.ItemsSource = null;
            TargetEditionBox.ItemsSource = null;
            Log(target.WritesImmediately ? "Changes written directly to the selected installation." : "Changes applied. Save & unmount to update the image file.");
        });
    }

    private async void Commit_Click(object sender, RoutedEventArgs e)
    {
        if (_installation is not null) { MessageBox.Show(this, "Deployed Windows changes are written immediately. Save and unmount applies to mounted image files only."); return; }
        if (_steps.Count > 0) { MessageBox.Show(this, "Apply or remove the remaining queued changes before saving. Queued changes have not been written to the image."); return; }
        if (MessageBox.Show(this, "Save every change in the mounted image to its source WIM and unmount?", "Save image", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await Unmount(true);
    }
    private async void Discard_Click(object sender, RoutedEventArgs e)
    {
        if (_installation is not null) { MessageBox.Show(this, "Deployed Windows changes cannot be discarded. Discard applies to mounted image files only."); return; }
        if (MessageBox.Show(this, "Discard ALL unsaved changes in the mounted image and unmount? This includes previously applied steps. The pending queue will be preserved.", "Discard image changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await Unmount(false);
    }
    private Task Unmount(bool commit) => Run(commit ? "Save image" : "Discard mount", async () =>
    {
        await _dism.UnmountAsync(RequireMount(), commit);
        _activeMount = null;
        _activeImage = null;
        _activeIndex = 0;
        FeatureList.ItemsSource = null;
        SyncLibrarySelection();
    });

    private void RemoveSteps_Click(object sender, RoutedEventArgs e)
    {
        foreach (var step in PlanList.SelectedItems.Cast<PlanStep>().ToArray()) _steps.Remove(step);
    }
    private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveStep(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveStep(1);
    private void MoveStep(int offset)
    {
        var index = PlanList.SelectedIndex;
        if (index >= 0 && index + offset >= 0 && index + offset < _steps.Count)
        { _steps.Move(index, index + offset); PlanList.SelectedIndex = index + offset; }
    }

    private async void SavePreset_Click(object sender, RoutedEventArgs e) => await Run("Save preset", async () =>
    {
        if (_installation is not null) throw new InvalidOperationException("Presets currently describe image-file projects. Select a WIM/ESD/VHD project before saving a preset.");
        if (_activeMount is not null && _activeImage is null)
            throw new InvalidOperationException("This mount was reconnected without source fields. Save its image and unmount, then read editions to create a preset.");
        var picker = new SaveFileDialog { Filter = "ImageForge preset|*.json", FileName = "imageforge-preset.json" };
        if (picker.ShowDialog(this) != true) { _operationCancelled = true; return; }
        var index = _activeIndex > 0 ? _activeIndex : (EditionBox.SelectedItem as ImageEdition)?.Index ?? 1;
        var preset = new ProjectPreset(_activeImage ?? ImagePathBox.Text.Trim(), index, _activeMount ?? MountPathBox.Text.Trim(), _steps.ToList());
        await File.WriteAllTextAsync(picker.FileName, JsonSerializer.Serialize(preset, JsonOptions));
        RememberPreset(picker.FileName);
        Log("Saved preset: " + picker.FileName);
    });

    private async void LoadPreset_Click(object sender, RoutedEventArgs e) => await Run("Load preset", async () =>
    {
        RequireUnmounted();
        var picker = FilePicker("ImageForge preset|*.json");
        if (picker.ShowDialog(this) != true) { _operationCancelled = true; return; }
        await LoadPresetPathAsync(picker.FileName);
    });

    private async Task LoadPresetPathAsync(string path)
    {
        RequireUnmounted();
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Preset exceeds the 1 MiB limit.");
        var preset = JsonSerializer.Deserialize<ProjectPreset>(await File.ReadAllTextAsync(path)) ?? throw new InvalidDataException("Invalid preset.");
        if (preset.Steps is null || preset.Steps.Count > 1000 || preset.ImageIndex < 1 || string.IsNullOrWhiteSpace(preset.ImagePath) || string.IsNullOrWhiteSpace(preset.MountPath))
            throw new InvalidDataException("Preset must have image and mount paths, a positive image index, and at most 1,000 operations.");
        if (preset.Steps.Any(s => s is null || s.Kind is not ("add-driver" or "add-package" or "enable-feature" or "disable-feature" or "set-edition") || string.IsNullOrWhiteSpace(s.Value)))
            throw new InvalidDataException("Preset contains unsupported or empty operations.");
        if (_steps.Count > 0 && MessageBox.Show(this, "Replace your current pending queue with this preset?", "Load preset", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        // Read the source before replacing the current project, so a bad preset leaves it intact.
        var editions = await _dism.GetEditionsAsync(preset.ImagePath);
        var edition = editions.FirstOrDefault(x => x.Index == preset.ImageIndex) ?? throw new InvalidDataException("Preset edition does not exist in that source image.");
        ImagePathBox.Text = preset.ImagePath;
        _installation = null;
        MountPathBox.Text = preset.MountPath;
        _editionsSource = Path.GetFullPath(preset.ImagePath);
        EditionBox.ItemsSource = editions;
        EditionBox.SelectedItem = edition;
        _steps.Clear();
        foreach (var step in preset.Steps) _steps.Add(step);
        RememberPreset(path);
        AddSourceRows(preset.ImagePath, editions);
        SelectLibraryEdition(_images.First(row => row.Path.Equals(Path.GetFullPath(preset.ImagePath), StringComparison.OrdinalIgnoreCase) && row.Index == edition.Index));
        Navigate(6);
        Log("Loaded preset. Review the queue before mounting and applying.");
    }

    private async void ExportUnattend_Click(object sender, RoutedEventArgs e) => await Run("Export answer file", async () =>
    {
        var xml = UnattendWriter.Generate(ComputerNameBox.Text.Trim(), LocaleBox.Text.Trim(), TimeZoneBox.Text.Trim());
        var picker = new SaveFileDialog { Filter = "Windows answer file|*.xml", FileName = "Autounattend.xml" };
        if (picker.ShowDialog(this) == true) { await File.WriteAllTextAsync(picker.FileName, xml, new UTF8Encoding(false)); Log("Answer file exported: " + picker.FileName); } else _operationCancelled = true;
    });
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_busy) { e.Cancel = true; MessageBox.Show(this, "An operation is running. Use Cancel download if available, or wait for it to finish before closing."); return; }
        if (_activeMount is not null || _steps.Count > 0)
        {
            var detail = _activeMount is not null ? $"The image will remain mounted at:\n{_activeMount}\n\nUse Reconnect owned mount next time, or cancel to save/discard it now." : "Your pending queue is not automatically saved. Cancel and save a preset to keep it.";
            if (MessageBox.Show(this, detail + "\n\nClose ImageForge?", "Leave workspace", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) e.Cancel = true;
        }
        if (!e.Cancel) SavePreferences();
    }
}
