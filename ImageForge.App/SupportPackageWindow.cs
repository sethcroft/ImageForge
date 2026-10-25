using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ImageForge.App;

internal sealed class SupportPackageWindow : Window
{
    public string? OutputPath { get; private set; }
    public string[] Presets { get; private set; } = [];
    public SupportPackageWindow(Window owner, string log)
    {
        Owner = owner; Title = "Send logs"; Width = 580; Height = 380; MinWidth = 500; MinHeight = 350; WindowStartupLocation = WindowStartupLocation.CenterOwner; FontSize = owner.FontSize;
        var root = new DockPanel { Margin = new(20) }; Content = root;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0,12,0,0) };
        var save = new Button { Content = "Create support ZIP…", IsDefault = true }; var cancel = new Button { Content = "Cancel", IsCancel = true }; actions.Children.Add(save); actions.Children.Add(cancel); DockPanel.SetDock(actions,Dock.Bottom); root.Children.Add(actions);
        var panel = new StackPanel(); root.Children.Add(panel);
        panel.Children.Add(new TextBlock { Text = "Prepare a support package", FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new(0,0,0,14) });
        panel.Children.Add(new TextBlock { Text = "The ZIP contains this session's diagnostic log and a version/OS summary. You can also select JSON presets to include. Logs and presets can contain personal information and local paths.\n\nNo crash dumps, hardware inventory or other application files are added automatically. Nothing is uploaded or emailed.", Margin = new(0,0,0,14) });
        panel.Children.Add(new TextBlock { Text = "Session log: " + System.IO.Path.GetFileName(log), FontSize = 11 });
        var count = new TextBlock { Text = "No presets selected", Margin = new(0,8,0,8) }; panel.Children.Add(count);
        var select = new Button { Content = "Choose optional presets…", HorizontalAlignment = HorizontalAlignment.Left }; panel.Children.Add(select);
        select.Click += (_, _) => { var picker = new OpenFileDialog { Filter = "ImageForge presets|*.json", Multiselect = true }; if (picker.ShowDialog(this) == true) { if (picker.FileNames.Length > 20) { MessageBox.Show(this,"Choose at most 20 presets."); return; } Presets = picker.FileNames; count.Text = $"{Presets.Length} preset(s) selected"; } };
        save.Click += (_, _) => { var picker = new SaveFileDialog { Filter = "Support ZIP|*.zip", FileName = "ImageForge-support-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip", OverwritePrompt = false }; if (picker.ShowDialog(this) == true) { OutputPath = picker.FileName; DialogResult = true; } };
    }
}
