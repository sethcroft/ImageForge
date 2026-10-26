using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ImageForge.Core;

namespace ImageForge.App;

public partial class MainWindow
{
    private IReadOnlyList<LibraryNode<ImageRow>> _libraryRoots = [];
    private bool _libraryReady, _rebuildingLibrary, _libraryHadSearch;
    private readonly Dictionary<string,bool> _libraryExpansion = new(StringComparer.OrdinalIgnoreCase);
    private LibraryNode<ImageRow>? _selectedLibraryNode;
    private void InitializeImageLibrary()
    {
        _libraryReady = true;
        RebuildImageLibrary();
    }
    private void RebuildImageLibrary()
    {
        if (!_libraryReady) return;
        if (!_libraryHadSearch) foreach (var node in ImageLibrary.Nodes(_libraryRoots)) _libraryExpansion[node.Key] = node.IsExpanded;
        var searching = !string.IsNullOrWhiteSpace(ImageFilterBox.Text);
        var selectedKey = _selectedLibraryNode?.Key;
        _rebuildingLibrary = true;
        try
        {
            var visibleRows = _images.Where(row =>
                (SourceFormatFilter.SelectedIndex == 0 || row.Format == (SourceFormatFilter.SelectedItem as ComboBoxItem)?.Content as string) &&
                (SourceStatusFilter.SelectedIndex == 0 || row.Status == (SourceStatusFilter.SelectedItem as ComboBoxItem)?.Content as string));
            _libraryRoots = ImageLibrary.Build(visibleRows.GroupBy(row => row.Path, StringComparer.OrdinalIgnoreCase)
                .Select(group => new LibrarySource<ImageRow>(group.Key, group.ToArray())), row => row.Index, row => row.Edition.Name,
                ImageFilterBox.Text, SourceKindFilter.SelectedIndex == 0 ? "All" : (SourceKindFilter.SelectedItem as ComboBoxItem)?.Content as string ?? "All");
            foreach (var folder in ImageLibrary.Nodes(_libraryRoots).Where(node => node.Kind == LibraryNodeKind.Folder))
                folder.Children.Sort((left, right) =>
                {
                    if (left.Kind != right.Kind) return left.Kind == LibraryNodeKind.Folder ? -1 : 1;
                    if (left.Kind == LibraryNodeKind.Source && (SourceSortBox.SelectedIndex > 0 || DateSortBox.SelectedIndex > 0))
                    {
                        var comparison = DateSortBox.SelectedIndex > 0
                            ? (DateSortBox.SelectedIndex == 1 ? 1 : -1) * System.IO.File.GetLastWriteTimeUtc(right.Path).CompareTo(System.IO.File.GetLastWriteTimeUtc(left.Path))
                            : (SourceSortBox.SelectedIndex == 1 ? 1 : -1) * _images.First(row => row.Path == right.Path).SizeBytes.CompareTo(_images.First(row => row.Path == left.Path).SizeBytes);
                        if (comparison != 0) return comparison;
                    }
                    return StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
                });
            _selectedLibraryNode = null;
            foreach (var node in ImageLibrary.Nodes(_libraryRoots))
            {
                if (searching) node.IsExpanded = true;
                else if (_libraryExpansion.TryGetValue(node.Key, out var expanded)) node.IsExpanded = expanded;
                if (node.Key.Equals(selectedKey, StringComparison.OrdinalIgnoreCase)) { node.IsSelected = true; _selectedLibraryNode = node; }
            }
            ImageList.ItemsSource = _libraryRoots;
            _libraryHadSearch = searching;
        }
        finally { _rebuildingLibrary = false; }
        SyncLibrarySelection();
    }
    private void SelectLibraryEdition(ImageRow? row)
    {
        // Explicit imports and preset selection must reveal their actual edition even if a filter hid it.
        if (row is not null && !ImageLibrary.Nodes(_libraryRoots).Any(node => ReferenceEquals(node.Edition, row)))
        {
            SourceKindFilter.SelectedIndex = 0;
            SourceFormatFilter.SelectedIndex = 0;
            SourceStatusFilter.SelectedIndex = 0;
            ImageFilterBox.Clear();
            RebuildImageLibrary();
        }
        _rebuildingLibrary = true;
        try
        {
            _selectedLibraryNode = null;
            foreach (var node in ImageLibrary.Nodes(_libraryRoots))
            {
                node.IsSelected = row is not null && ReferenceEquals(node.Edition, row);
                if (node.IsSelected) _selectedLibraryNode = node;
                if (row is not null && (node.Kind is LibraryNodeKind.Category or LibraryNodeKind.Folder or LibraryNodeKind.Source) &&
                    (node.Kind == LibraryNodeKind.Category ? node.Name == row.Category :
                     row.Path.Equals(node.Path, StringComparison.OrdinalIgnoreCase) || row.Path.StartsWith(node.Path.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                    node.IsExpanded = true;
            }
        }
        finally { _rebuildingLibrary = false; }
        SyncLibrarySelection();
    }
    private void ImageSelection_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_rebuildingLibrary) return;
        _selectedLibraryNode = e.NewValue as LibraryNode<ImageRow>;
        SyncLibrarySelection();
    }
    private void SyncLibrarySelection()
    {
        if (_activeMount is not null) return;
        if (_selectedLibraryNode?.Edition is not { } row)
        {
            if (_installation is null) { ImagePathBox.Clear(); EditionBox.ItemsSource = null; _editionsSource = null; }
            MountImageButton.IsEnabled = false;
            return;
        }
        if (!SwitchToImageProject()) { SelectLibraryEdition(null); return; }
        ImagePathBox.Text = row.Path; _editionsSource = row.Path;
        EditionBox.ItemsSource = _images.Where(item => item.Path.Equals(row.Path, StringComparison.OrdinalIgnoreCase)).Select(item => item.Edition).ToArray();
        EditionBox.SelectedItem = row.Edition;
        UpdateMountBadge();
    }
    private static TreeViewItem? ClosestTreeItem(DependencyObject? source)
    {
        while (source is not null && source is not TreeViewItem)
            source = source is Visual || source is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        return source as TreeViewItem;
    }
}
