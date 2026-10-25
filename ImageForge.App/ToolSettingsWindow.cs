using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ImageForge.App;

internal sealed record ToolSettings
{
    public bool LargerFonts { get; init; }
    public bool ShowInfoBar { get; init; } = true;
    public bool ShowDescriptions { get; init; }
    public bool CompactDates { get; init; } = true;
    public bool CheckDaily { get; init; }
    public DateTimeOffset? LastCheckUtc { get; init; }
    public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImageForge", "tool-settings.json");
    public static ToolSettings Load()
    {
        if (!File.Exists(FilePath)) return new();
        if (new FileInfo(FilePath).Length > 1024 * 1024) throw new IOException("Tool settings exceed the size limit.");
        return JsonSerializer.Deserialize<ToolSettings>(File.ReadAllText(FilePath)) ?? new();
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, FilePath, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

internal sealed class ToolSettingsWindow : Window
{
    private readonly StackPanel sections = new();
    private readonly List<(FrameworkElement Element, string Keywords)> searchable = [];
    private readonly CheckBox large, info, descriptions, dates;
    private readonly TextBox dism;
    public ToolSettings Result { get; private set; }
    public string DismPath => dism.Text.Trim();
    public ToolSettingsWindow(Window owner, ToolSettings settings, string engine, bool imageMounted)
    {
        Owner = owner; Title = "Tool settings"; Width = 710; Height = 730; MinWidth = 610; MinHeight = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Result = settings; FontSize = settings.LargerFonts ? 14 : 12;
        var root = new DockPanel(); Content = root;
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(14) };
        var ok = new Button { Content = "OK", Width = 78, IsDefault = true };
        var cancel = new Button { Content = "Cancel", Width = 78, IsCancel = true };
        footer.Children.Add(ok); footer.Children.Add(cancel); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var filter = new TextBox { Margin = new(16,12,16,12), ToolTip = "Filter tool settings by name", MinHeight = 30 };
        System.Windows.Automation.AutomationProperties.SetName(filter, "Filter tool settings");
        DockPanel.SetDock(filter, Dock.Top); root.Children.Add(filter);
        root.Children.Add(new ScrollViewer { Content = sections, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        filter.TextChanged += (_, _) => { foreach (var (element, keywords) in searchable) element.Visibility = keywords.Contains(filter.Text.Trim(), StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed; };

        var appearance = Section("Appearance", "language visual style larger fonts info bar");
        Row(appearance, "Language", FixedChoice("English (Default)", "English is the only available translation."));
        Row(appearance, "Visual style", FixedChoice("Light", "Additional themes are planned."));
        large = Toggle(settings.LargerFonts); Row(appearance, "Use larger fonts", large);
        info = Toggle(settings.ShowInfoBar); Row(appearance, "Show info bar", info);
        var reset = new Button { Content = "Reset", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left, ToolTip = "There are no suppressible warning preferences in this preview." };
        ToolTipService.SetShowOnDisabled(reset, true); Row(appearance, "Reset warnings", reset);
        var lists = Section("Lists", "descriptions columns compact last change dates actions hover expand");
        descriptions = Toggle(settings.ShowDescriptions); Row(lists, "Show descriptions as columns", descriptions);
        Row(lists, "Show actions on hover", UnavailableToggle("Hover actions are planned; use the page toolbar."));
        Row(lists, "Modified rows", FixedChoice("Status column", "Modified state is displayed in the status column."));
        Row(lists, "Expand on title click", FixedChoice("Expander header", "Use the category or source expander header."));
        dates = Toggle(settings.CompactDates); Row(lists, "Compact 'Last change' dates", dates);
        var presets = Section("Presets", "save load auto saved history backup boot wim default removal");
        foreach (var label in new[] { "Save last session as 'Auto-saved'", "Load last session 'Auto-saved'", "Keep history in overwritten presets", "Back up preset and log on edited image", "Include boot.wim updates by default" })
            Row(presets, label, UnavailableToggle("Planned. Presets are saved and imported explicitly in this preview."));
        Row(presets, "Default removal level", FixedChoice("Not available", "Component removal is planned."));
        var performance = Section("Performance", "threads compression verify images integrity");
        Row(performance, "Threads", FixedChoice("Automatic", "The Windows servicing tools choose their own parallelism."));
        Row(performance, "ESD compression threads", FixedChoice("Automatic", "DISM controls ESD compression."));
        var verify = Toggle(true); verify.IsEnabled = false; verify.ToolTip = "Integrity flags are retained for supported WIM operations. Microsoft download SHA-256 verification is mandatory. VHD operations use their supported flags.";
        Row(performance, "Verify image files", verify);
        var storage = Section("Storage", "data folder diagnostics downloads edit cache directory free space");
        FolderRow(storage, "Data folder", Path.GetDirectoryName(ToolSettings.FilePath)!);
        FolderRow(storage, "Diagnostics", Path.Combine(Path.GetDirectoryName(ToolSettings.FilePath)!, "Diagnostics"));
        var editCache = Toggle(true); editCache.IsEnabled = false; editCache.ToolTip = "ESD/SWM preparation uses the fixed per-user image cache.";
        Row(storage, "Edit cache", editCache);
        FolderRow(storage, "Directory", Path.Combine(Path.GetDirectoryName(ToolSettings.FilePath)!, "Images"));
        Row(storage, "Downloads", new TextBlock { Text = "Choose destination for each download", Foreground = Brushes.Gray });
        Row(storage, "Available space", new TextBlock { Text = FreeSpace(), Foreground = Brushes.Gray });
        var servicing = Section("DISM", "engine contact windows update debug log");
        var enginePanel = new DockPanel();
        var browse = new Button { Content = "…", IsEnabled = !imageMounted }; DockPanel.SetDock(browse, Dock.Right); enginePanel.Children.Add(browse);
        dism = new TextBox { Text = engine, IsReadOnly = imageMounted, Margin = new(0), Padding = new(4), ToolTip = imageMounted ? "Unmount the image before changing its servicing engine." : "Empty uses Windows DISM; select a compatible ADK dism.exe." }; enginePanel.Children.Add(dism);
        browse.Click += (_, _) => { var picker = new OpenFileDialog { Filter = "DISM engine|dism.exe", CheckFileExists = true }; if (picker.ShowDialog(this) == true) dism.Text = picker.FileName; };
        Row(servicing, "Engine (empty = Windows default)", enginePanel);
        Row(servicing, "Contact Windows Update", FixedChoice("Offline / LimitAccess", "Feature enabling uses the existing offline /LimitAccess policy."));
        Row(servicing, "Log at debug level", UnavailableToggle("Normal per-session diagnostics are always saved; debug-level selection is planned."));
        var network = Section("Network", "proxy address username password");
        Row(network, "Proxy", FixedChoice("System default", "Downloads and release checks use the system HTTP proxy. Custom proxy settings are planned."));
        Row(network, "Address", new TextBox { IsEnabled = false, Margin = new(0), Padding = new(3) });
        Row(network, "Username", new TextBox { IsEnabled = false, Margin = new(0), Padding = new(3) });
        Row(network, "Password", new PasswordBox { IsEnabled = false, Padding = new(3) });
        ok.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(dism.Text) && (!File.Exists(DismPath) || !Path.GetFileName(DismPath).Equals("dism.exe", StringComparison.OrdinalIgnoreCase)))
            { MessageBox.Show(this, "Select an existing ADK dism.exe or leave the engine empty.", "Tool settings"); return; }
            Result = settings with { LargerFonts = large.IsChecked == true, ShowInfoBar = info.IsChecked == true, ShowDescriptions = descriptions.IsChecked == true, CompactDates = dates.IsChecked == true };
            DialogResult = true;
        };
    }
    private static CheckBox Toggle(bool value) => new() { IsChecked = value, Style = (Style)Application.Current.FindResource("ToggleSwitch"), VerticalAlignment = VerticalAlignment.Center };
    private static CheckBox UnavailableToggle(string tooltip) { var control = Toggle(false); control.IsEnabled = false; control.ToolTip = tooltip; ToolTipService.SetShowOnDisabled(control, true); return control; }
    private static ComboBox FixedChoice(string text, string tooltip)
    {
        var combo = new ComboBox { IsEnabled = false, Margin = new(0), Padding = new(4,1,4,1), Width = 190, HorizontalAlignment = HorizontalAlignment.Left, ToolTip = tooltip };
        combo.Items.Add(text); combo.SelectedIndex = 0; ToolTipService.SetShowOnDisabled(combo, true); return combo;
    }
    private StackPanel Section(string title, string keywords)
    {
        var panel = new StackPanel { Margin = new(8) };
        var expander = new Expander { Header = title, Content = panel, IsExpanded = true, Background = new SolidColorBrush(Color.FromRgb(250,250,250)), Margin = new(14,0,14,14), Padding = new(4) };
        sections.Children.Add(expander); searchable.Add((expander, title + " " + keywords)); return panel;
    }
    private static void Row(Panel section, string label, FrameworkElement control)
    {
        var grid = new Grid { Margin = new(0,3,0,3), MinHeight = 23 };
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(250) }); grid.ColumnDefinitions.Add(new());
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        System.Windows.Automation.AutomationProperties.SetName(control, label);
        Grid.SetColumn(control, 1); grid.Children.Add(control); section.Children.Add(grid);
    }
    private void FolderRow(Panel section, string label, string path)
    {
        var button = new Button { Content = path, HorizontalContentAlignment = HorizontalAlignment.Left, ToolTip = "Open folder · Storage paths are fixed in this preview", Padding = new(4) };
        button.Click += (_, _) => { try { Directory.CreateDirectory(path); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } };
        Row(section, label, button);
    }
    private static string FreeSpace()
    {
        try { return $"{new DriveInfo(Path.GetPathRoot(ToolSettings.FilePath)!).AvailableFreeSpace / 1024d / 1024d / 1024d:N1} GB on data drive"; }
        catch (IOException) { return "Not available"; }
    }
}
