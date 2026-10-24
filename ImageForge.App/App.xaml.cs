using System.Windows;
namespace ImageForge.App;
public partial class App : Application
{
    private Mutex? _instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        var user = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        _instance = new Mutex(true, @"Global\ImageForge.OfflineWorkshop." + user, out var created);
        if (!created)
        {
            MessageBox.Show("ImageForge is already open for this Windows account. Use the existing window.", "ImageForge");
            Shutdown();
            return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message + "\n\nPreserve the mount journal in %LOCALAPPDATA%\\ImageForge if recovery is needed.", "ImageForge error");
            args.Handled = true;
            Shutdown(1);
        };
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }
}
