using System.Windows;
using System.Windows.Controls;

namespace ImageForge.App;

public partial class MainWindow
{
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();
    private void UnmountMenu_Click(object sender, RoutedEventArgs e) => ShowCommandMenu((Button)sender,
        [("Save changes and unmount", Commit_Click), ("Discard changes and unmount…", Discard_Click)]);
    private void ExportMenu_Click(object sender, RoutedEventArgs e) => ShowCommandMenu((Button)sender,
        [("Export / convert image…", ExportEdition_Click), ("Split WIM…", SplitImage_Click), ("Image and ISO tools", ShowTools_Click)]);
    private static void ShowCommandMenu(Button owner, (string Label, RoutedEventHandler Handler)[] entries)
    {
        var menu = new ContextMenu { PlacementTarget = owner };
        foreach (var entry in entries) { var item = new MenuItem { Header = entry.Label }; item.Click += entry.Handler; menu.Items.Add(item); }
        menu.IsOpen = true;
    }
    private void SizeSort_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SourceSortBox?.SelectedIndex > 0 && DateSortBox is not null) DateSortBox.SelectedIndex = 0;
        RebuildImageLibrary();
    }
    private void DateSort_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (DateSortBox?.SelectedIndex > 0 && SourceSortBox is not null) SourceSortBox.SelectedIndex = 0;
        RebuildImageLibrary();
    }
    private void LibraryFilter_Changed(object sender, SelectionChangedEventArgs e) => RebuildImageLibrary();
}
