using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ImageForge.Core;
using Microsoft.Win32;

namespace ImageForge.App;

public partial class MainWindow
{
    private ToolSettings _toolSettings = new();
    internal static string ProgramVersion => typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.5.0-preview.1";
    private void ApplicationMenu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = ApplicationMenuButton, Placement = PlacementMode.Bottom, MinWidth = 196, Background = new SolidColorBrush(Color.FromRgb(248,248,248)), Padding = new(5) };
        void Item(string icon, string text, RoutedEventHandler handler, string tooltip, bool enabled = true)
        {
            var item = new MenuItem { Header = text, Icon = new TextBlock { Text = icon, Foreground = new SolidColorBrush(Color.FromRgb(41,148,180)), FontSize = 16 }, ToolTip = tooltip, IsEnabled = enabled, Padding = new(8,7,8,7) };
            ToolTipService.SetShowOnDisabled(item, true); item.Click += handler; menu.Items.Add(item);
        }
        Item("↗", "New instance", (_, _) => { }, "ImageForge currently permits one instance per Windows account to protect its mount journal. Use New session in the toolbar.", false);
        Item("⚙", "Settings", ToolSettings_Click, "Configure appearance, lists, storage information and the DISM engine.");
        Item("↻", "Check for updates", CheckUpdates_Click, "Check ImageForge releases on GitHub. Updates are downloaded and installed manually.");
        Item("⚿", "Manage license", License_Click, "View ImageForge's MIT license. No activation key or subscription is required.");
        Item("▤", "Export HW list", ExportHardware_Click, "Export this PC's hardware names, categories and device status to an ImageForge JSON file. NTLite imports are not supported.");
        Item("✉", "Send logs", SendLogs_Click, "Create a local support ZIP with this session's log and optional presets you select. Nothing is sent automatically.");
        Item("?", "About", About_Click, "Show ImageForge program version, project information and license.");
        menu.Items.Add(new Separator());
        Item("⏻", "Exit", (_, _) => Close(), "Close ImageForge using its normal mounted-image and pending-queue checks.");
        menu.IsOpen = true;
    }
    private void LoadToolSettings()
    {
        try { _toolSettings = ToolSettings.Load(); }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        { Log("Tool settings could not be loaded: " + ex.Message); }
        ApplyToolSettings();
        Loaded += async (_, _) =>
        {
            if (!_toolSettings.CheckDaily || DateTimeOffset.UtcNow - (_toolSettings.LastCheckUtc ?? DateTimeOffset.MinValue) < TimeSpan.FromDays(1)) return;
            try
            {
                var catalog = await ReleaseCatalog.FetchAsync();
                SaveUpdatePreference(_toolSettings.CheckDaily, DateTimeOffset.UtcNow);
                var newest = new[] { catalog.Stable, catalog.Preview }.OfType<ToolRelease>().OrderDescending().FirstOrDefault();
                if (newest?.CompareTo(ToolRelease.Parse(ProgramVersion)) > 0 && !_busy)
                    StatusText.Text = "ImageForge update available · " + newest.Tag + " · Application menu → Check for updates";
            }
            catch (Exception ex) { Log("Startup release check failed: " + ex.Message); }
        };
    }
    private void ApplyToolSettings()
    {
        FontSize = _toolSettings.LargerFonts ? 14 : 12;
        PageHint.Visibility = _toolSettings.ShowInfoBar ? Visibility.Visible : Visibility.Collapsed;
        ImageDescriptionColumn.Width = _toolSettings.ShowDescriptions ? 240 : 0;
        foreach (var row in _images) { row.CompactDates = _toolSettings.CompactDates; row.RefreshPresentation(); }
        foreach (var preset in _presets) { preset.CompactDates = _toolSettings.CompactDates; preset.RefreshPresentation(); }
    }
    private void ToolSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ToolSettingsWindow(this, _toolSettings, DismPathBox.Text.Trim(), _activeMount is not null);
        if (dialog.ShowDialog() != true) return;
        try
        {
            dialog.Result.Save(); _toolSettings = dialog.Result;
            DismPathBox.Text = dialog.DismPath; SavePreferences(); ApplyToolSettings();
        }
        catch (Exception ex) { MessageBox.Show(this, "Tool settings could not be saved.\n\n" + ex.Message, "Tool settings"); }
    }
    private void SaveUpdatePreference(bool daily, DateTimeOffset? checkedUtc = null)
    {
        var next = _toolSettings with { CheckDaily = daily, LastCheckUtc = checkedUtc ?? _toolSettings.LastCheckUtc };
        try { next.Save(); _toolSettings = next; }
        catch (Exception ex) { Log("Update options were not saved: " + ex.Message); MessageBox.Show(this, "Update options could not be saved.", "ImageForge"); }
    }
    private void CheckUpdates_Click(object sender, RoutedEventArgs e) => new UpdateCheckWindow(this, _toolSettings.CheckDaily, SaveUpdatePreference).ShowDialog();
    private void About_Click(object sender, RoutedEventArgs e)
    {
        var panel = new StackPanel { Margin = new(22) };
        panel.Children.Add(new TextBlock { Text = "ImageForge", FontSize = 25, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(32,132,155)) });
        panel.Children.Add(new TextBlock { Text = "Windows Image Workshop\nVersion " + ProgramVersion + " · Windows x64\n.NET " + Environment.Version, Margin = new(0,12,0,16) });
        panel.Children.Add(new TextBlock { Text = "An independent Windows image-servicing preview using Microsoft's DISM tools. Inspired by image-management workflows; not affiliated with or endorsed by NTLite or Microsoft.\n\nDistributed under the MIT license. See the included license for its copyright notice and terms.", Margin = new(0,0,0,16) });
        var links = new WrapPanel();
        var project = new Button { Content = "Project on GitHub ↗" }; project.Click += OpenGitHub_Click; links.Children.Add(project);
        var license = new Button { Content = "View license" }; license.Click += License_Click; links.Children.Add(license); panel.Children.Add(links);
        ShowInformationDialog("About ImageForge", panel, 510, 380);
    }
    private void License_Click(object sender, RoutedEventArgs e)
    {
        using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("ImageForge.LICENSE")!;
        using var reader = new StreamReader(stream);
        var panel = new StackPanel { Margin = new(18) };
        panel.Children.Add(new TextBlock { Text = "MIT license · No activation required", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new(0,0,0,12) });
        panel.Children.Add(new TextBlock { Text = "This ImageForge preview is open source. There is no license key, expiry date, paid subscription or remote activation. Windows and Microsoft tool licensing remains separate.", Margin = new(0,0,0,16) });
        panel.Children.Add(new TextBox { Text = reader.ReadToEnd(), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Height = 255, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        ShowInformationDialog("Manage license", panel, 580, 490);
    }
    private void ShowInformationDialog(string title, FrameworkElement content, double width, double height)
    {
        var window = new Window { FontSize = FontSize, Owner = this, Title = title, Width = width, Height = height, MinWidth = 420, MinHeight = 300, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var root = new DockPanel(); var close = new Button { Content = "Close", IsCancel = true, IsDefault = true, Width = 80, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(12) };
        close.Click += (_, _) => window.Close(); DockPanel.SetDock(close, Dock.Bottom); root.Children.Add(close);
        root.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); window.Content = root; window.ShowDialog();
    }
    private async void ExportHardware_Click(object sender, RoutedEventArgs e)
    {
        var picker = new SaveFileDialog { Filter = "ImageForge hardware list|*.json", FileName = "ImageForge-hardware.json", OverwritePrompt = false };
        if (picker.ShowDialog(this) != true) return;
        await Run("Exporting hardware list", async () =>
        {
            if (File.Exists(picker.FileName)) throw new IOException("Choose a new hardware-list filename.");
            var json = await HardwareInventory.ReadJsonAsync();
            await using var file = new FileStream(picker.FileName, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await file.WriteAsync(Encoding.UTF8.GetBytes(json));
            MessageBox.Show(this, "Hardware list saved.\n\nThis is an ImageForge JSON report; NTLite checklist import is not supported.", "Export HW list");
        });
    }
    private async void SendLogs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SupportPackageWindow(this, _diagnostics.SessionPath);
        if (dialog.ShowDialog() != true) return;
        await Run("Creating support package", async () =>
        {
            await Task.Run(() => SupportPackage.Create(dialog.OutputPath!, ProgramVersion, _diagnostics.SessionPath, dialog.Presets));
            MessageBox.Show(this, "Support ZIP created locally:\n" + dialog.OutputPath + "\n\nNothing has been uploaded or emailed. Review the files and local paths before sharing.", "Send logs");
        });
    }
}
