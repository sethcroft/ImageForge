namespace ImageForge.Core;

public static class MediaTemplate
{
    public static async Task CopyAsync(string source, string destination, IProgress<DownloadProgress>? progress = null)
    {
        source = DismCommands.SafePath(source); destination = DismCommands.SafePath(destination);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException("Choose matching Windows setup media as a template.");
        if (File.Exists(destination) || Directory.Exists(destination) || destination.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a new media-folder path outside the template folder.");
        if (!Directory.Exists(Path.GetDirectoryName(destination))) throw new DirectoryNotFoundException("The new media folder's parent does not exist.");
        foreach (var relative in new[] { "setup.exe", "sources/boot.wim", "boot/etfsboot.com", "efi/microsoft/boot/efisys.bin" })
            if (!File.Exists(Path.Combine(source, relative))) throw new FileNotFoundException("The template is missing a required Windows setup or boot file.", relative);
        var files = new List<(string Path, string Relative)>(); var directories = new Queue<string>(); directories.Enqueue(source);
        long total = 0;
        while (directories.TryDequeue(out var directory))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("The media template cannot contain directory links or junctions.");
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("The media template cannot contain linked files.");
                var relative = Path.GetRelativePath(source, file);
                var parent = Path.GetDirectoryName(relative);
                if (string.Equals(parent, "sources", StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(file).StartsWith("install", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(file).ToLowerInvariant() is ".wim" or ".esd" or ".swm") continue;
                files.Add((file, relative)); total = checked(total + new FileInfo(file).Length);
                if (files.Count > 50000) throw new ArgumentException("The selected template contains too many files. Choose the Windows setup-media folder only.");
            }
            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                if (directories.Count > 10000) throw new ArgumentException("The media template contains too many folders.");
                directories.Enqueue(child);
            }
        }
        Directory.CreateDirectory(destination);
        long copied = 0; var buffer = new byte[1024 * 1024];
        var lastProgress = System.Diagnostics.Stopwatch.StartNew();
        foreach (var file in files)
        {
            var output = Path.Combine(destination, file.Relative); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await using var input = File.OpenRead(file.Path);
            await using var target = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, true);
            int count;
            while ((count = await input.ReadAsync(buffer)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, count)); copied += count;
                if (lastProgress.ElapsedMilliseconds >= 150 || copied == total)
                { progress?.Report(new(copied, total)); lastProgress.Restart(); }
            }
        }
    }
}
