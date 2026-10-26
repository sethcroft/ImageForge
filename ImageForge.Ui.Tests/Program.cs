using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ImageForge.App;
using ImageForge.Core;

internal static class Program
{
    private static readonly List<string> errors = [];
    private static int checks;
    [STAThread]
    private static int Main()
    {
        var output = Path.GetFullPath("artifacts/windows-ui"); Directory.CreateDirectory(output);
        var app = new App(); app.InitializeComponent(); app.StartupUri = null; app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        // Drive the real WPF dispatcher without invoking App.OnStartup's fatal-error dialog.
        app.DispatcherUnhandledException += (_, e) => { errors.Add(e.Exception.ToString()); e.Handled = true; };
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingErrors());
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        var task = RunAsync(app, output);
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => app.Dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        try { task.GetAwaiter().GetResult(); File.WriteAllText(Path.Combine(output, "result.txt"), $"{checks} Windows WPF runtime checks passed. Actual DISM capture/read; no Windows installation was serviced.\n"); return 0; }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "failure.txt"), ex + "\n" + string.Join("\n", errors)); Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); }
    }
    private static async Task RunAsync(App app, string output)
    {
        var fixture = Path.Combine(Path.GetTempPath(), "ImageForge-ui-" + Guid.NewGuid().ToString("N"));
        var capture = Path.Combine(fixture, "capture"); var media = Path.Combine(fixture, "extracted-ISO");
        Directory.CreateDirectory(capture); Directory.CreateDirectory(Path.Combine(media, "sources"));
        File.WriteAllText(Path.Combine(capture,"fixture.txt"), "UI regression fixture; not a Windows installation.");
        MainWindow? window = null;
        try
        {
            await DismAsync(["/English", "/Capture-Image", "/CaptureDir:" + capture, "/ImageFile:" + Path.Combine(media,"sources","install.wim"), "/Name:ImageForge UI fixture", "/Compress:fast", "/CheckIntegrity"]);
            File.Copy(Path.Combine(media,"sources","install.wim"), Path.Combine(media,"sources","boot.wim"));
            window = new MainWindow(); app.MainWindow = window; window.Show(); await Settle(window);
            Check(errors.Count == 0, "startup has no dispatcher or binding errors");
            await (Task)Invoke(window, "ScanFolderAsync", media)!; await Settle(window);
            Check(errors.Count == 0, "adding extracted ISO directory completes without read-only binding exceptions");
            var rows = ((IEnumerable)Field(window,"_images")!).Cast<object>().ToArray();
            Check(rows.Length == 2, "actual DISM inspection discovers install.wim and boot.wim editions");
            var tree = (TreeView)window.FindName("ImageList");
            Check(Visuals<TreeViewItem>(tree).Any(item => item.Header is not null), "realized tree containers render after directory import");
            foreach (var bar in Visuals<ProgressBar>(tree))
            {
                var binding = BindingOperations.GetBinding(bar, ProgressBar.ValueProperty);
                Check(binding?.Mode == BindingMode.OneWay, "realized progress bars use OneWay bindings");
            }
            Invoke(rows[0], "SetActivity", (double?)37.5); await Settle(window);
            Check(Visuals<ProgressBar>(tree).Any(bar => Math.Abs(bar.Value - 37.5) < .01), "progress reaches a real rendered control from a read-only model property");
            Check(errors.Count == 0, "progress updates create no dispatcher or binding errors");
            Capture(window, Path.Combine(output,"image-library-1280.png"));
            var brand = (FrameworkElement)window.FindName("BrandMark"); var point = brand.TransformToAncestor(window).Transform(new Point(brand.ActualWidth/2, 0));
            Check(Math.Abs(point.X-window.ActualWidth/2)<2, "branding is centered in the actual window");
            var search = (TextBox)window.FindName("ImageFilterBox"); search.Text = "does-not-exist"; await Settle(window);
            Check(!Visuals<TextBlock>(tree).Any(text => text.Text == "1 · ImageForge UI fixture"), "search hides nonmatching edition rows");
            search.Clear(); await Settle(window);
            Check(Visuals<TextBlock>(tree).Any(text => text.Text == "1 · ImageForge UI fixture"), "clearing search restores real edition rows");
            var format = (ComboBox)window.FindName("SourceFormatFilter"); format.SelectedIndex=2; await Settle(window);
            Check(!Visuals<TextBlock>(tree).Any(text => text.Text == "1 · ImageForge UI fixture"), "format filter excludes WIM when ESD is chosen");
            format.SelectedIndex=0; await Settle(window);
            window.Width=1120; await Settle(window); Capture(window,Path.Combine(output,"image-library-1120.png"));
            Check(((FrameworkElement)window.FindName("PresetPane")).ActualWidth >= 300, "presets remain visible at minimum window width");
            Check(errors.Count == 0, "search, filters and resizing retain valid WPF bindings");
        }
        finally { window?.Close(); Directory.Delete(fixture,true); }
    }
    private static object? Invoke(object target,string name,params object?[] args) => target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.Invoke(target,args);
    private static object? Field(object target,string name) => target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target);
    private static void Check(bool result,string text) { if (!result) throw new Exception("FAILED: " + text + "\n" + string.Join("\n", errors)); checks++; Console.WriteLine("PASS " + text); }
    private static async Task Settle(Window window) { window.UpdateLayout(); await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle); await Task.Delay(80); window.UpdateLayout(); }
    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        if(root is T match) yield return match;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var child in Visuals<T>(VisualTreeHelper.GetChild(root,i))) yield return child;
    }
    private static void Capture(Window window,string path)
    {
        var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(window);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file=File.Create(path); encoder.Save(file);
    }
    private static async Task DismAsync(string[] arguments)
    {
        var start=new ProcessStartInfo("dism.exe") { UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true };
        foreach(var arg in arguments) start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!; var stdout=process.StandardOutput.ReadToEndAsync(); var stderr=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(2)); await process.WaitForExitAsync(timeout.Token);
        var text=await stdout + await stderr; if(process.ExitCode!=0) throw new Exception("Fixture capture failed: " + text);
    }
    private sealed class BindingErrors : TraceListener
    {
        public override void Write(string? message) { if (!string.IsNullOrWhiteSpace(message)) errors.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
}
