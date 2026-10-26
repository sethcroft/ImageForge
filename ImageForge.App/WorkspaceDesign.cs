using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ImageForge.Core;

namespace ImageForge.App;

public partial class MainWindow
{
    private static bool IsEditingPage(int page) => WorkspaceNavigation.IsEditingPage(page);
    private bool HasLoadedTarget => _activeMount is not null || _installation is not null;
    private bool _navigating;

    private void Navigate(int page)
    {
        page = WorkspaceNavigation.ResolvePage(page, HasLoadedTarget);
        _navigating = true;
        try
        {
            NavigationList.SelectedItem = NavigationList.Items.Cast<ListBoxItem>().FirstOrDefault(item => item.Tag as string == page.ToString());
            WorkspaceTabs.SelectedIndex = page;
            UpdatePageChrome(page);
        }
        finally { _navigating = false; }
    }

    private void UpdatePageChrome(int page)
    {
        if (PresetPane is null) return;
        var library = page == 0;
        PresetPane.Visibility = library ? Visibility.Visible : Visibility.Collapsed;
        PresetColumn.MinWidth = library ? 250 : 0;
        PresetColumn.Width = library ? new GridLength(310) : new GridLength(0);
        ImageCommands.Visibility = library ? Visibility.Visible : Visibility.Collapsed;
        PageCommands.Visibility = library ? Visibility.Collapsed : Visibility.Visible;
        ContextAddButton.IsEnabled = page is 1 or 2 or 3 or 4 or 5 or 6 or 8 or 9;
        ContextSecondaryButton.IsEnabled = page is 1 or 3 or 5 or 6;
        (ContextAddButton.Content, ContextSecondaryButton.Content, PageHint.Text) = page switch
        {
            0 => ("＋ Add", "Refresh", HasLoadedTarget ? "Use page navigation on the left to edit the loaded Windows target. Save or unmount before switching image files." : "Use an existing image below, or add a file, directory, or Windows download."),
            1 => ("＋ Add INF files", "▰ Directory", "Add signed INF drivers to the queue. Offline images and other Windows drives only."),
            2 => ("＋ Add packages", "Refresh", "Add applicable update packages to the queue in prerequisite order."),
            3 => ("↻ Read features", "＋ Queue enable", "Read optional features from the loaded target, then select features to enable or disable."),
            4 => ("↗ Export answer file", "Refresh", "Set computer name, locale and time zone for a partial amd64 answer file."),
            5 => ("↗ Export / convert", "◉ Create ISO", "Convert, split and rebuild media using new output paths. Unmount images first."),
            6 => ("▶ Apply queue", "↑ Move up", "Review pending changes before processing. Save or discard applies to mounted image files only."),
            7 => ("＋ Add", "Refresh", "Review Windows host requirements, target formats and preview limitations."),
            8 => ("↓ Download", "Refresh", "Download official Microsoft media using its direct link and exact SHA-256 hash."),
            9 => ("↻ Scan compatibility", "Refresh", "Use original matching Microsoft media to check this PC before opening interactive Windows Setup."),
            _ => ("＋ Add", "Refresh", "Design preview · This section is planned and does not change the Windows target.")
        };
    }

    private void UpdateEditingNavigation()
    {
        var visibility = HasLoadedTarget ? Visibility.Visible : Visibility.Collapsed;
        foreach (var item in NavigationList.Items.Cast<ListBoxItem>())
            if ((item.Tag is string tag && int.TryParse(tag, out var page) && IsEditingPage(page)) ||
                item == IntegrateHeading || item == RemoveHeading || item == ConfigureHeading || item == FinishHeading)
                item.Visibility = visibility;
        if (!HasLoadedTarget && IsEditingPage(WorkspaceTabs.SelectedIndex)) Navigate(0);
        else UpdatePageChrome(WorkspaceTabs.SelectedIndex);
        FooterTarget.Text = _installation?.DisplayName ?? (_activeMount is null ? "" : (EditionBox.SelectedItem as ImageEdition)?.Name ?? "Loaded Windows image");
    }

    private void InitializeDesignQueues()
    {
        var driverView = new ListCollectionView(_steps) { Filter = item => item is PlanStep { Kind: "add-driver" } };
        var packageView = new ListCollectionView(_steps) { Filter = item => item is PlanStep { Kind: "add-package" } };
        DriverQueue.ItemsSource = driverView;
        UpdateQueue.ItemsSource = packageView;
        CollectionViewSource.GetDefaultView(_presets).Filter = value => value is PresetEntry preset &&
            (string.IsNullOrWhiteSpace(PresetFilterBox.Text) || preset.Name.Contains(PresetFilterBox.Text, StringComparison.OrdinalIgnoreCase));
    }

    private void SourceKind_Changed(object sender, SelectionChangedEventArgs e)
    {
        RebuildImageLibrary();
    }
    private void PresetFilter_Changed(object sender, TextChangedEventArgs e)
    {
        if (PresetList?.ItemsSource is not null) CollectionViewSource.GetDefaultView(_presets).Refresh();
    }
    private void BackToImage_Click(object sender, RoutedEventArgs e) => Navigate(0);
    private void ReviewQueue_Click(object sender, RoutedEventArgs e) => Navigate(6);
    private void CreateIsoPage_Click(object sender, RoutedEventArgs e) => Navigate(5);
    private void ContextAdd_Click(object sender, RoutedEventArgs e)
    {
        switch (WorkspaceTabs.SelectedIndex)
        {
            case 1: AddDrivers_Click(sender, e); break;
            case 2: AddPackages_Click(sender, e); break;
            case 3: RefreshFeatures_Click(sender, e); break;
            case 4: ExportUnattend_Click(sender, e); break;
            case 5: ExportEdition_Click(sender, e); break;
            case 6: Apply_Click(sender, e); break;
            case 8: DownloadWindows_Click(sender, e); break;
            case 9: ScanRefresh_Click(sender, e); break;
        }
    }
    private void ContextSecondary_Click(object sender, RoutedEventArgs e)
    {
        switch (WorkspaceTabs.SelectedIndex)
        {
            case 1: AddDriverFolder_Click(sender, e); break;
            case 3: EnableFeatures_Click(sender, e); break;
            case 5: CreateIso_Click(sender, e); break;
            case 6: MoveUp_Click(sender, e); break;
        }
    }
    private void AddMenu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = (Button)sender };
        foreach (var (label, handler) in new (string, RoutedEventHandler)[]
        {
            ("Image file…", AddImage_Click), ("Directory…", AddDirectory_Click),
            ("Import ISO…", ImportIso_Click), ("Download Windows…", ShowDownloads_Click)
        })
        {
            var item = new MenuItem { Header = label }; item.Click += handler; menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }
    private void NewSession_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMount is not null)
        {
            MessageBox.Show(this, "Save or unmount the loaded image before starting a new session.", "New session"); return;
        }
        if (_steps.Count > 0 && MessageBox.Show(this, "Clear the pending queue and start a new session? Save a preset first to keep these changes.", "New session", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _steps.Clear(); _installation = null; _editionsSource = null;
        SelectLibraryEdition(null); ImagePathBox.Clear(); EditionBox.ItemsSource = null;
        FeatureList.ItemsSource = null; TargetEditionBox.ItemsSource = null;
        Navigate(0); UpdateMountBadge(); StatusText.Text = "Ready · New session" + (_restartRequired ? " · Restart required" : "");
    }
    private void Image_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ClosestTreeItem(source)?.DataContext is LibraryNode<ImageRow> { Kind: LibraryNodeKind.Edition } && !_busy && _activeMount is null)
            Mount_Click(sender, e);
    }
    private void Preset_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(PresetList, source) is ListBoxItem && !_busy)
            LoadSelectedPreset_Click(sender, e);
    }
}
