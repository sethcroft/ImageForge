using System.Windows;
namespace ImageForge.App;
public partial class App : Application
{
    private Mutex? _instance;
    private readonly FatalUiErrorState fatalUiError = new();
    internal bool FatalShutdownRequested => fatalUiError.Requested;
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
            args.Handled = true;
            if (!fatalUiError.BeginShutdown()) return;
            try { (MainWindow as ImageForge.App.MainWindow)?.RecordFatalError(args.Exception); } catch { }
            try
            {
                MessageBox.Show("ImageForge encountered an interface error and needs to close.\n\nDetails are in %LOCALAPPDATA%\\ImageForge\\Diagnostics. Preserve the mount journal if recovery is needed. ImageForge will not automatically save or discard an image.", "ImageForge error");
            }
            finally { Shutdown(1); }
        };
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }
}
