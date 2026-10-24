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
    private bool _busy;
    private string? _activeMount;
    private string? _editionsSource;
    private string? _activeImage;
    private int _activeIndex;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public MainWindow()
    {
        InitializeComponent();
        _dism = new DismService(Log);
        InitializeWorkspace();
        PlanList.ItemsSource = _steps;
        _steps.CollectionChanged += (_, _) => QueueBadge.Text = $"{_steps.Count} change{(_steps.Count == 1 ? "" : "s")} queued";
        MountPathBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImageForge", "Mount");
        Log("ImageForge 0.2 — offline image and ISO workspace. No image operation runs until requested.");
    }

    private void Log(string text)
    {
        Dispatcher.Invoke(() =>
        {
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            if (LogBox.Text.Length > 1_000_000)
                LogBox.Text = "[Earlier activity trimmed; DISM's full service log remains in Windows\\Logs\\DISM.]" + Environment.NewLine + LogBox.Text[^750_000..];
            LogBox.ScrollToEnd();
        });
    }

    private async Task Run(string title, Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        WorkspaceBody.IsEnabled = false;
        CommandRibbon.IsEnabled = false;
        TopMenu.IsEnabled = false;
        BusyProgress.Visibility = Visibility.Visible;
        StatusText.Text = title;
        try
        {
            _dism.ExecutableOverride = string.IsNullOrWhiteSpace(DismPathBox.Text) ? null : DismPathBox.Text.Trim();
            await action();
            StatusText.Text = "Ready · " + title + " completed";
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            ActivityExpander.IsExpanded = true;
            StatusText.Text = "Stopped · " + ex.Message;
            MessageBox.Show(this, ex.Message + "\n\nSee the activity log. A failed operation may have produced partial output or changed the mounted image. Inspect it before retrying.", "Operation stopped", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _busy = false;
            WorkspaceBody.IsEnabled = true;
            CommandRibbon.IsEnabled = true;
            TopMenu.IsEnabled = true;
            BusyProgress.Visibility = Visibility.Collapsed;
            UpdateMountBadge();
        }
    }

    private void UpdateMountBadge()
    {
        MountBadge.Text = _activeMount is null ? "No image mounted" : "Image mounted · Offline";
        DismPathBox.IsReadOnly = _activeMount is not null;
        ImagePathBox.IsReadOnly = _activeMount is not null;
        EditionBox.IsEnabled = _activeMount is null;
        foreach (var row in _images) row.Mounted = _activeMount is not null && row.Path.Equals(_activeImage, StringComparison.OrdinalIgnoreCase);
        ImageList.Items.Refresh();
    }
    private string RequireMount() => _activeMount ?? throw new InvalidOperationException("Mount an image or reconnect an ImageForge-owned mount first.");
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
        await _dism.MountAsync(image, edition.Index, mount);
        _activeMount = mount;
        _activeImage = image;
        _activeIndex = edition.Index;
        Log("Mounted. Queue drivers, updates, or optional feature changes, then open Apply.");
    });

    private async void Reconnect_Click(object sender, RoutedEventArgs e) => await Run("Reconnect mount", async () =>
    {
        RequireUnmounted();
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
        var picker = FilePicker("Driver INF files|*.inf", true);
        if (picker.ShowDialog(this) == true) foreach (var file in picker.FileNames) AddStep(new("add-driver", file));
    }
    private void AddDriverFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Driver folder — includes subfolders" };
        if (picker.ShowDialog(this) == true) AddStep(new("add-driver", picker.FolderName));
    }
    private void AddPackages_Click(object sender, RoutedEventArgs e)
    {
        var picker = FilePicker("Windows update packages|*.cab;*.msu", true);
        if (picker.ShowDialog(this) == true) foreach (var file in picker.FileNames) AddStep(new("add-package", file));
    }

    private async void RefreshFeatures_Click(object sender, RoutedEventArgs e) => await Run("Read optional features", async () =>
        FeatureList.ItemsSource = await _dism.GetFeaturesAsync(RequireMount()));
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
        if (MessageBox.Show(this, $"Apply {_steps.Count} queued change(s) to the mounted offline image?\n\nChanges remain in the mount until you save or discard. Servicing may take several minutes.", "Apply changes", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await Run("Apply changes", async () =>
        {
            var mount = RequireMount();
            foreach (var step in _steps) DismCommands.ForStep(mount, step);
            foreach (var step in _steps.ToArray())
            {
                await _dism.ApplyStepAsync(mount, step);
                _steps.Remove(step);
            }
            FeatureList.ItemsSource = null;
            Log("Queued changes applied. Choose Save & unmount to update the WIM.");
        });
    }

    private async void Commit_Click(object sender, RoutedEventArgs e)
    {
        if (_steps.Count > 0) { MessageBox.Show(this, "Apply or remove the remaining queued changes before saving. Queued changes have not been written to the image."); return; }
        if (MessageBox.Show(this, "Save every change in the mounted image to its source WIM and unmount?", "Save image", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await Unmount(true);
    }
    private async void Discard_Click(object sender, RoutedEventArgs e)
    {
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
        if (_activeMount is not null && _activeImage is null)
            throw new InvalidOperationException("This mount was reconnected without source fields. Save its image and unmount, then read editions to create a preset.");
        var picker = new SaveFileDialog { Filter = "ImageForge preset|*.json", FileName = "imageforge-preset.json" };
        if (picker.ShowDialog(this) != true) return;
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
        if (picker.ShowDialog(this) != true) return;
        await LoadPresetPathAsync(picker.FileName);
    });

    private async Task LoadPresetPathAsync(string path)
    {
        RequireUnmounted();
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Preset exceeds the 1 MiB limit.");
        var preset = JsonSerializer.Deserialize<ProjectPreset>(await File.ReadAllTextAsync(path)) ?? throw new InvalidDataException("Invalid preset.");
        if (preset.Steps is null || preset.Steps.Count > 1000 || preset.ImageIndex < 1 || string.IsNullOrWhiteSpace(preset.ImagePath) || string.IsNullOrWhiteSpace(preset.MountPath))
            throw new InvalidDataException("Preset must have image and mount paths, a positive image index, and at most 1,000 operations.");
        if (preset.Steps.Any(s => s is null || s.Kind is not ("add-driver" or "add-package" or "enable-feature" or "disable-feature") || string.IsNullOrWhiteSpace(s.Value)))
            throw new InvalidDataException("Preset contains unsupported or empty operations.");
        if (_steps.Count > 0 && MessageBox.Show(this, "Replace your current pending queue with this preset?", "Load preset", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        // Read the source before replacing the current project, so a bad preset leaves it intact.
        var editions = await _dism.GetEditionsAsync(preset.ImagePath);
        var edition = editions.FirstOrDefault(x => x.Index == preset.ImageIndex) ?? throw new InvalidDataException("Preset edition does not exist in that source image.");
        ImagePathBox.Text = preset.ImagePath;
        MountPathBox.Text = preset.MountPath;
        _editionsSource = Path.GetFullPath(preset.ImagePath);
        EditionBox.ItemsSource = editions;
        EditionBox.SelectedItem = edition;
        _steps.Clear();
        foreach (var step in preset.Steps) _steps.Add(step);
        RememberPreset(path);
        AddSourceRows(preset.ImagePath, editions);
        Navigate(6);
        Log("Loaded preset. Review the queue before mounting and applying.");
    }

    private async void ExportUnattend_Click(object sender, RoutedEventArgs e) => await Run("Export answer file", async () =>
    {
        var xml = UnattendWriter.Generate(ComputerNameBox.Text.Trim(), LocaleBox.Text.Trim(), TimeZoneBox.Text.Trim());
        var picker = new SaveFileDialog { Filter = "Windows answer file|*.xml", FileName = "Autounattend.xml" };
        if (picker.ShowDialog(this) == true) { await File.WriteAllTextAsync(picker.FileName, xml, new UTF8Encoding(false)); Log("Answer file exported: " + picker.FileName); }
    });
    private async void ExportLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new SaveFileDialog { Filter = "Activity log|*.txt", FileName = "imageforge-log.txt" };
            if (picker.ShowDialog(this) == true) await File.WriteAllTextAsync(picker.FileName, LogBox.Text);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not export log"); }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_busy) { e.Cancel = true; MessageBox.Show(this, "Wait for the current servicing operation to finish before closing."); return; }
        if (_activeMount is not null || _steps.Count > 0)
        {
            var detail = _activeMount is not null ? $"The image will remain mounted at:\n{_activeMount}\n\nUse Reconnect owned mount next time, or cancel to save/discard it now." : "Your pending queue is not automatically saved. Cancel and save a preset to keep it.";
            if (MessageBox.Show(this, detail + "\n\nClose ImageForge?", "Leave workspace", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) e.Cancel = true;
        }
        if (!e.Cancel) SavePreferences();
    }
}
