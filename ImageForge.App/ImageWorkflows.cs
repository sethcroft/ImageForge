using System.IO;
using System.Security.Cryptography;
using System.Windows;
using ImageForge.Core;
using Microsoft.Win32;

namespace ImageForge.App;

public partial class MainWindow
{
    private string? _refreshFingerprint;
    private bool _restartRequired;
    private void ShowDownloads_Click(object sender, RoutedEventArgs e) => Navigate(8);
    private void ShowRefresh_Click(object sender, RoutedEventArgs e) => Navigate(9);
    private void OpenWindows11_Click(object sender, RoutedEventArgs e) => OpenLink("https://www.microsoft.com/software-download/windows11");
    private void OpenWindows10_Click(object sender, RoutedEventArgs e) => OpenLink("https://www.microsoft.com/software-download/windows10ISO");
    private void CancelDownload_Click(object sender, RoutedEventArgs e) => _downloadCancellation?.Cancel();

    private bool SwitchToImageProject()
    {
        if (_installation is null) return true;
        if (_steps.Count > 0 && MessageBox.Show(this, "Switching from a deployed installation to an image file clears the pending queue. Continue?", "Change target", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return false;
        _steps.Clear(); _installation = null; FeatureList.ItemsSource = null; TargetEditionBox.ItemsSource = null;
        UpdateMountBadge();
        return true;
    }
    private bool CanSelectInstallation()
    {
        RequireUnmounted();
        if (_steps.Count > 0 && MessageBox.Show(this, "Changing the target installation clears the pending queue. Continue?", "Change target", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return false;
        _steps.Clear(); SelectLibraryEdition(null); ImagePathBox.Clear(); EditionBox.ItemsSource = null; _editionsSource = null;
        TargetEditionBox.ItemsSource = null; return true;
    }
    private async void SelectLive_Click(object sender, RoutedEventArgs e) => await Run("Loading running Windows", async () =>
    {
        if (!CanSelectInstallation()) return;
        _installation = ServicingTarget.Running; _activitySource = null;
        FeatureList.ItemsSource = await _dism.GetFeaturesAsync(_installation);
    });
    private async void SelectDeployed_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Choose the root of a different Windows drive, such as D:\\" };
        if (picker.ShowDialog(this) != true) return;
        await Run("Loading deployed Windows", async () =>
        {
            var target = _dism.SelectDeployedVolume(picker.FolderName);
            if (!CanSelectInstallation()) return;
            _installation = target; _activitySource = null;
            FeatureList.ItemsSource = await _dism.GetFeaturesAsync(target);
        });
    }

    private async void DownloadWindows_Click(object sender, RoutedEventArgs e)
    {
        var picker = new SaveFileDialog { Title = "Save verified Microsoft Windows media to a NEW file", Filter = "Windows ISO|*.iso|Windows ESD|*.esd", FileName = "Windows.iso" };
        if (picker.ShowDialog(this) != true) return;
        using var cancellation = new CancellationTokenSource(); _downloadCancellation = cancellation;
        try
        {
            await Run("Downloading Windows", async () =>
            {
                _activitySource = null; CancelDownloadButton.Visibility = Visibility.Visible;
                using var downloader = new MicrosoftMediaDownload();
                var progress = new Progress<DownloadProgress>(value =>
                {
                    ReportActivity("Downloading Windows", value.Percentage);
                    if (_busy && value.TotalBytes is null) StatusText.Text = $"Downloading Windows · {value.BytesReceived / 1024d / 1024d:N0} MB";
                });
                await downloader.DownloadAsync(DownloadUrlBox.Text.Trim(), DownloadHashBox.Text.Trim(), picker.FileName, progress, cancellation.Token);
                ReportActivity("Download verified", 100);
                if (Path.GetExtension(picker.FileName).Equals(".esd", StringComparison.OrdinalIgnoreCase) && _activeMount is null)
                    await AddSourceAsync(picker.FileName);
                MessageBox.Show(this, "Microsoft media downloaded and SHA-256 verified.\n\n" + picker.FileName + "\n\nFor ISO files, choose Import ISO to open the media.", "Download complete");
            });
        }
        finally { _downloadCancellation = null; }
    }

    private async void DeleteEdition_Click(object sender, RoutedEventArgs e)
    {
        if (EditionBox.SelectedItem is not ImageEdition edition) { MessageBox.Show(this, "Select an image edition first."); return; }
        var source = ImagePathBox.Text.Trim();
        if (MessageBox.Show(this, $"Permanently delete edition {edition.Index} ({edition.Name}) from this source WIM?\n\n{source}\n\nThis changes the file directly and cannot be discarded. Work on a copy.", "Delete image edition", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await Run("Removing image edition", async () =>
        {
            RequireUnmounted(); _activitySource = source;
            await _dism.DeleteEditionAsync(source, edition.Index);
            await AddSourceAsync(source);
        });
    }
    private async void ReadTargetEditions_Click(object sender, RoutedEventArgs e) => await Run("Reading eligible editions", async () =>
    {
        var editions = await _dism.GetTargetEditionsAsync(EffectiveTarget);
        TargetEditionBox.ItemsSource = editions; TargetEditionBox.SelectedIndex = editions.Count > 0 ? 0 : -1;
        if (editions.Count == 0) MessageBox.Show(this, "Windows reports no eligible edition upgrades for this image.");
    });
    private void QueueEdition_Click(object sender, RoutedEventArgs e)
    {
        if (TargetEditionBox.SelectedItem is not string edition) { MessageBox.Show(this, "Load an offline image and read its eligible upgrades first."); return; }
        foreach (var old in _steps.Where(s => s.Kind == "set-edition").ToArray()) _steps.Remove(old);
        AddStep(new("set-edition", edition));
    }
    private async void ConvertImageToIso_Click(object sender, RoutedEventArgs e)
    {
        var folder = new SaveFileDialog { Title = "Choose a parent and type a NEW working-media folder name", Filter = "New media folder|*", FileName = "ImageForge-media", AddExtension = false };
        if (folder.ShowDialog(this) != true) return;
        var output = new SaveFileDialog { Title = "Save the converted bootable ISO to a NEW file", Filter = "Windows ISO|*.iso", FileName = "ImageForge.iso" };
        if (output.ShowDialog(this) != true) return;
        await Run("Converting image to ISO", async () =>
        {
            RequireUnmounted();
            if (EditionBox.SelectedItem is not ImageEdition edition || !ImagePathBox.Text.Trim().Equals(_editionsSource, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Select a WIM/ESD/SWM image and read its editions before converting.");
            var source = DismCommands.ExistingFile(ImagePathBox.Text.Trim(), ".wim", ".esd", ".swm");
            ImageTools.NewOutputFile(output.FileName, ".iso");
            if (output.FileName.StartsWith(folder.FileName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Save the ISO outside the new working-media folder.");
            if (await _dism.HasMountedSourceUnderAsync(MediaFolderBox.Text.Trim())) throw new ArgumentException("Unmount images under the template media folder before converting.");
            if (!File.Exists(OscdimgPathBox.Text.Trim())) throw new FileNotFoundException("Select the Microsoft ADK oscdimg.exe tool first.");
            _activitySource = source;
            await MediaTemplate.CopyAsync(MediaFolderBox.Text.Trim(), folder.FileName,
                new Progress<DownloadProgress>(value => ReportActivity("Preparing Windows setup media", value.Percentage)));
            var image = Path.Combine(folder.FileName, "sources", "install.wim");
            if (ExportAllCheck.IsChecked == true) await _dism.ExportAllEditionsAsync(source, image);
            else await _dism.ExportEditionAsync(source, edition.Index, image);
            ReportActivity("Saving ISO", null);
            await ImageTools.CreateIsoAsync(OscdimgPathBox.Text.Trim(), folder.FileName, output.FileName, IsoLabelBox.Text.Trim(), Log);
            MediaFolderBox.Text = folder.FileName;
            await AddSourceAsync(image);
        });
    }

    private void BrowseRefreshMedia_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Choose original, unmodified Microsoft setup media" };
        if (picker.ShowDialog(this) == true) RefreshMediaBox.Text = picker.FolderName;
    }
    private void RefreshMedia_Changed(object sender, RoutedEventArgs e)
    {
        _refreshFingerprint = null;
        if (HostLaunchButton is not null) HostLaunchButton.IsEnabled = false;
        if (RefreshResultText is not null) RefreshResultText.Text = "Run a compatibility scan for the selected original Microsoft media.";
    }
    private static string RefreshFingerprint(string media)
    {
        var setup = HostRefresh.SetupPath(media);
        using var stream = File.OpenRead(setup);
        var install = new FileInfo(File.Exists(Path.Combine(media, "sources", "install.wim")) ? Path.Combine(media, "sources", "install.wim") : Path.Combine(media, "sources", "install.esd"));
        return Path.GetFullPath(media) + ":" + Convert.ToHexString(SHA256.HashData(stream)) + ":" + install.Length + ":" + install.LastWriteTimeUtc.Ticks;
    }
    private async void ScanRefresh_Click(object sender, RoutedEventArgs e) => await Run("Checking upgrade compatibility", async () =>
    {
        RequireUnmounted();
        _activitySource = null; _refreshFingerprint = null; HostLaunchButton.IsEnabled = false;
        if (OriginalMediaCheck.IsChecked != true) throw new ArgumentException("Confirm that the selected media is original Microsoft media with matching architecture and system UI language.");
        var media = DismCommands.SafePath(RefreshMediaBox.Text.Trim());
        if (await _dism.HasMountedSourceUnderAsync(media)) throw new ArgumentException("Unmount the selected media's image files before running Windows Setup.");
        var fingerprint = RefreshFingerprint(media);
        var result = await HostRefresh.ScanAsync(media, Log);
        RefreshResultText.Text = result.Message;
        if (result.CanProceed && fingerprint == RefreshFingerprint(media))
        { _refreshFingerprint = fingerprint; HostLaunchButton.IsEnabled = true; }
    });
    private async void LaunchRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Open Microsoft Windows Setup for this PC?\n\nReview its license terms and preservation choices before installing. Use a current backup. ImageForge will not start an unattended upgrade or choose a clean installation.", "Host refresh", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await Run("Opening Windows Setup", async () =>
        {
            RequireUnmounted(); _activitySource = null;
            var media = DismCommands.SafePath(RefreshMediaBox.Text.Trim());
            if (OriginalMediaCheck.IsChecked != true || _refreshFingerprint is null || _refreshFingerprint != RefreshFingerprint(media))
                throw new ArgumentException("The media or confirmation has changed. Run the compatibility scan again.");
            await HostRefresh.LaunchInteractiveAsync(media, Log);
            _refreshFingerprint = null; HostLaunchButton.IsEnabled = false;
        });
    }
}
