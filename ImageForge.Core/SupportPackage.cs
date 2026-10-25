using System.IO.Compression;
using System.Text;

namespace ImageForge.Core;

public static class SupportPackage
{
    public static void Create(string output, string version, string logPath, IReadOnlyList<string> presets)
    {
        if (presets.Count > 20) throw new ArgumentException("Choose at most 20 presets.");
        var destination = Path.GetFullPath(output);
        if (File.Exists(destination)) throw new IOException("Choose a new support ZIP filename.");
        var partial = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using (var archive = ZipFile.Open(partial, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(archive.CreateEntry("README.txt").Open(), Encoding.UTF8))
                    writer.Write($"ImageForge {version}\nCreated UTC: {DateTime.UtcNow:O}\nOS: {Environment.OSVersion}\n\nLocal support package. Nothing has been uploaded or emailed.\nThe session log and any explicitly selected presets can include local paths or personal data. Review before sharing.\nNo memory dumps, credentials, mount journal or hardware inventory are automatically included.\n");
                Add(logPath, "diagnostics/session.log", 20 * 1024 * 1024);
                for (var index = 0; index < presets.Count; index++)
                {
                    if (!Path.GetExtension(presets[index]).Equals(".json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Select JSON preset files only.");
                    Add(presets[index], $"presets/preset-{index + 1:00}.json", 1024 * 1024);
                }
                void Add(string path, string entryName, long maxBytes)
                {
                    using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (input.Length > maxBytes) throw new IOException("A selected support file exceeds its size limit.");
                    // Bound the copy even if an active log grows while it is being read.
                    var remaining = input.Length;
                    using var target = archive.CreateEntry(entryName, CompressionLevel.Optimal).Open();
                    var buffer = new byte[65536];
                    while (remaining > 0)
                    {
                        var count = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                        if (count == 0) break;
                        target.Write(buffer, 0, count); remaining -= count;
                    }
                }
            }
            File.Move(partial, destination, false);
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
}
