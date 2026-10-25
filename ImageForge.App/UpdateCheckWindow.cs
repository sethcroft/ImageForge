using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ImageForge.Core;

namespace ImageForge.App;

internal sealed class UpdateCheckWindow : Window
{
    private readonly TextBlock stable = new() { Text = "Not checked" }, preview = new() { Text = "Not checked" }, status = new() { Text = "Checking releases…", Foreground = Brushes.Gray };
    private readonly Button stableOpen = new() { Content = "Open release", IsEnabled = false }, previewOpen = new() { Content = "Open release", IsEnabled = false }, check = new() { Content = "Check now" };
    private readonly CancellationTokenSource cancellation = new();
    private readonly Action<bool,DateTimeOffset?> save;
    private readonly CheckBox daily;
    private ToolRelease? stableRelease, previewRelease;
    public UpdateCheckWindow(Window owner, bool checkDaily, Action<bool,DateTimeOffset?> savePreference)
    {
        Owner = owner; Title = "Check for updates"; Width = 750; Height = 440; MinWidth = 650; MinHeight = 400; WindowStartupLocation = WindowStartupLocation.CenterOwner; save = savePreference; FontSize = owner.FontSize;
        var root = new DockPanel { Margin = new(18) }; Content = root;
        var footer = new DockPanel { Margin = new(0,15,0,0) }; var close = new Button { Content = "Close", IsCancel = true, Width = 80, HorizontalAlignment = HorizontalAlignment.Right }; close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Right); footer.Children.Add(close); footer.Children.Add(status); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var columns = new Grid(); columns.ColumnDefinitions.Add(new() { Width = new GridLength(1,GridUnitType.Star) }); columns.ColumnDefinitions.Add(new() { Width = new GridLength(1,GridUnitType.Star) }); root.Children.Add(columns);
        var tool = new StackPanel { Margin = new(0,0,20,0) }; columns.Children.Add(tool);
        tool.Children.Add(new TextBlock { Text = "Tool · ImageForge", FontWeight = FontWeights.SemiBold, Margin = new(0,0,0,12) });
        Row(tool, "Installed", new TextBlock { Text = MainWindow.ProgramVersion });
        Row(tool, "Stable", stable); tool.Children.Add(stableOpen);
        Row(tool, "Preview", preview); tool.Children.Add(previewOpen);
        check.Margin = new(0,12,0,12); check.HorizontalAlignment = HorizontalAlignment.Left; tool.Children.Add(check);
        tool.Children.Add(new TextBlock { Text = "Host · " + Environment.OSVersion.VersionString, FontWeight = FontWeights.SemiBold });
        var windows = new Button { Content = "Open Windows Update", Margin = new(0,8,0,0), HorizontalAlignment = HorizontalAlignment.Left };
        windows.Click += (_, _) => Open("ms-settings:windowsupdate"); tool.Children.Add(windows);
        var options = new StackPanel(); Grid.SetColumn(options,1); columns.Children.Add(options);
        options.Children.Add(new TextBlock { Text = "Options", FontWeight = FontWeights.SemiBold, Margin = new(0,0,0,12) });
        daily = new CheckBox { IsChecked = checkDaily, Content = "Check daily on tool startup", Margin = new(0,0,0,16) }; options.Children.Add(daily);
        daily.Checked += (_, _) => save(true, null); daily.Unchecked += (_, _) => save(false, null);
        options.Children.Add(new CheckBox { Content = "Update tool automatically", IsChecked = false, IsEnabled = false, Margin = new(0,0,0,18), ToolTip = "Automatic download and installation are planned." });
        options.Children.Add(new TextBlock { Text = "Checks read public GitHub release metadata. Opening a release takes you to GitHub to review and download it.\n\nThis preview does not replace itself or install updates automatically. Host updates are managed by Windows Settings.", Foreground = Brushes.Gray });
        check.Click += async (_, _) => await CheckAsync();
        stableOpen.Click += (_, _) => { if (stableRelease is not null) Open(stableRelease.Page.AbsoluteUri); };
        previewOpen.Click += (_, _) => { if (previewRelease is not null) Open(previewRelease.Page.AbsoluteUri); };
        Loaded += async (_, _) => await CheckAsync();
        Closed += (_, _) => cancellation.Cancel();
    }
    private static void Row(Panel panel, string label, FrameworkElement value)
    {
        var row = new DockPanel { Margin = new(0,5,0,7) }; row.Children.Add(new TextBlock { Text = label, Width = 80, Foreground = Brushes.Gray }); row.Children.Add(value); panel.Children.Add(row);
    }
    private async Task CheckAsync()
    {
        check.IsEnabled = false; stableOpen.IsEnabled = previewOpen.IsEnabled = false; status.Text = "Checking GitHub releases…";
        try
        {
            var catalog = await ReleaseCatalog.FetchAsync(cancellation.Token);
            if (cancellation.IsCancellationRequested) return;
            stableRelease = catalog.Stable; previewRelease = catalog.Preview;
            stable.Text = stableRelease?.Tag ?? "Not available"; preview.Text = previewRelease?.Tag ?? "Not available";
            stableOpen.IsEnabled = stableRelease is not null; previewOpen.IsEnabled = previewRelease is not null;
            var installed = ToolRelease.Parse(MainWindow.ProgramVersion);
            var newer = new[] { stableRelease, previewRelease }.OfType<ToolRelease>().Any(release => release.CompareTo(installed) > 0);
            status.Text = newer ? "A newer ImageForge release is available." : "No newer published ImageForge version found.";
            save(daily.IsChecked == true, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception) { stable.Text = preview.Text = "Unavailable"; status.Text = "Could not check releases. Check your connection or retry later (GitHub may limit requests)."; }
        finally { check.IsEnabled = true; }
    }
    private void Open(string address)
    {
        try { Process.Start(new ProcessStartInfo(address) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not open link"); }
    }
}
