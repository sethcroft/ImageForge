using System.IO;
using System.Text;

namespace ImageForge.App;

internal sealed class DiagnosticLog : IDisposable
{
    private readonly object sync = new();
    private readonly StreamWriter writer;
    private long characters;
    private bool unavailable;
    public bool Available => !unavailable;
    public string DirectoryPath { get; }
    public DiagnosticLog()
    {
        DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImageForge", "Diagnostics");
        Directory.CreateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath, $"imageforge-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log");
        writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
    }
    public void Write(string text)
    {
        lock (sync)
        {
            if (unavailable || characters > 16 * 1024 * 1024) return;
            try
            {
                writer.WriteLine($"[{DateTime.Now:O}] {text}"); characters += text.Length;
                if (characters > 16 * 1024 * 1024) writer.WriteLine("Session diagnostic size limit reached.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
            { unavailable = true; }
        }
    }
    public void Dispose()
    {
        lock (sync)
        {
            unavailable = true;
            try { writer.Dispose(); } catch (IOException) { }
        }
    }
}
