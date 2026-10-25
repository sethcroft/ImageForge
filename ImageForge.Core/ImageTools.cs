using System.Text;
using System.Text.RegularExpressions;

namespace ImageForge.Core;

public sealed record ImageDiscovery(IReadOnlyList<string> Paths, int SkippedDirectories, bool LimitReached);

public static class ImageTools
{
    public static string NewOutputFile(string path, string extension)
    {
        var full = DismCommands.SafePath(path);
        if (!string.Equals(Path.GetExtension(full), extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Choose a {extension} output file.");
        if (File.Exists(full) || Directory.Exists(full)) throw new IOException("The output already exists. Choose a new file; existing output is never overwritten.");
        if (!Directory.Exists(Path.GetDirectoryName(full))) throw new DirectoryNotFoundException("The output's parent folder does not exist.");
        if (Path.GetFileName(full).IndexOfAny(['*', '?']) >= 0) throw new ArgumentException("Output file names cannot contain wildcards.");
        return full;
    }

    public static IReadOnlyList<string> ExportArguments(string image, int index, string destination)
    {
        image = DismCommands.ExistingFile(image, ".wim", ".esd", ".swm");
        if (index < 1) throw new ArgumentException("Select a positive edition index.");
        var extension = Path.GetExtension(destination).ToLowerInvariant();
        if (extension is not (".wim" or ".esd")) throw new ArgumentException("Export to a new .wim or .esd file.");
        destination = NewOutputFile(destination, extension);
        var args = new List<string> { "/English", "/Export-Image", $"/SourceImageFile:{image}", $"/SourceIndex:{index}",
            $"/DestinationImageFile:{destination}", $"/Compress:{(extension == ".esd" ? "recovery" : "max")}", "/CheckIntegrity" };
        if (Path.GetExtension(image).Equals(".swm", StringComparison.OrdinalIgnoreCase)) args.Add("/SWMFile:" + SplitWimPattern(image));
        return args;
    }

    public static IReadOnlyList<string> MountArguments(string image, int index, string mount)
    {
        image = DismCommands.ExistingFile(image, ".wim", ".vhd", ".vhdx");
        mount = DismCommands.SafePath(mount);
        var virtualDisk = Path.GetExtension(image).ToLowerInvariant() is ".vhd" or ".vhdx";
        if (index < 1 || virtualDisk && index != 1) throw new ArgumentException("Choose a valid image index. VHD/VHDX images use index 1.");
        var args = new List<string> { "/English", "/Mount-Image", $"/ImageFile:{image}", $"/Index:{index}", $"/MountDir:{mount}" };
        if (!virtualDisk) args.Add("/CheckIntegrity");
        return args;
    }

    public static string SplitWimPattern(string firstPart)
    {
        firstPart = DismCommands.ExistingFile(firstPart, ".swm");
        byte[] Header(string path)
        {
            using var stream = File.OpenRead(path); var data = new byte[44]; stream.ReadExactly(data);
            if (!data.AsSpan(0, 8).SequenceEqual("MSWIM\0\0\0"u8)) throw new InvalidDataException("A split-set file has an invalid WIM header.");
            return data;
        }
        var first = Header(firstPart);
        var count = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(first.AsSpan(42));
        if (System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(first.AsSpan(40)) != 1 || count is < 1 or > 4096)
            throw new ArgumentException("Select the first SWM part, such as install.swm, with every part in the same folder.");
        var prefix = Path.GetFileNameWithoutExtension(firstPart);
        if (prefix.IndexOfAny(['*', '?']) >= 0) throw new ArgumentException("Split-set filenames cannot contain wildcards.");
        var pattern = Path.Combine(Path.GetDirectoryName(firstPart)!, prefix + "*.swm");
        var parts = new HashSet<ushort>();
        foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(firstPart)!, prefix + "*.swm"))
        {
            var header = Header(file); var number = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(40));
            if (!header.AsSpan(24, 16).SequenceEqual(first.AsSpan(24, 16)) ||
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(42)) != count || number < 1 || number > count || !parts.Add(number))
                throw new InvalidDataException("The selected SWM files contain mixed, duplicated, or invalid parts.");
        }
        if (parts.Count != count) throw new InvalidDataException("The SWM set is incomplete. Keep every original part in the same folder.");
        return pattern;
    }

    public static IReadOnlyList<string> SplitArguments(string image, string destination, int sizeMb)
    {
        image = DismCommands.ExistingFile(image, ".wim");
        destination = NewOutputFile(destination, ".swm");
        if (sizeMb is < 64 or > 4096) throw new ArgumentException("Split size must be between 64 and 4096 MB.");
        var prefix = Path.GetFileNameWithoutExtension(destination);
        if (Directory.EnumerateFiles(Path.GetDirectoryName(destination)!).Any(p =>
            Path.GetExtension(p).Equals(".swm", StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileNameWithoutExtension(p).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("That output folder already contains similarly named SWM files. Choose an empty output folder.");
        return ["/English", "/Split-Image", $"/ImageFile:{image}", $"/SWMFile:{destination}", $"/FileSize:{sizeMb}", "/CheckIntegrity"];
    }

    public static IReadOnlyList<string> IsoArguments(string mediaDirectory, string destination, string label)
    {
        var media = DismCommands.SafePath(mediaDirectory);
        if (!Directory.Exists(media)) throw new DirectoryNotFoundException("Select an extracted Windows installation-media folder.");
        destination = NewOutputFile(destination, ".iso");
        if (destination.StartsWith(media + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Save the ISO outside its source-media folder.");
        if (!Regex.IsMatch(label ?? "", "^[A-Za-z0-9_-]{1,32}$"))
            throw new ArgumentException("ISO label must be 1–32 letters, digits, underscores or hyphens.");
        var bios = Path.Combine(media, "boot", "etfsboot.com");
        var uefi = Path.Combine(media, "efi", "microsoft", "boot", "efisys.bin");
        foreach (var path in new[] { bios, uefi, Path.Combine(media, "sources", "boot.wim"), Path.Combine(media, "setup.exe") })
            if (!File.Exists(path)) throw new FileNotFoundException("Required Windows setup-media file is missing.", path);
        if (!new[] { "install.wim", "install.esd", "install.swm" }.Any(p => File.Exists(Path.Combine(media, "sources", p))))
            throw new FileNotFoundException("The source-media folder needs sources/install.wim, install.esd, or install.swm.");
        if (media.IndexOfAny(['#', ',']) >= 0)
            throw new ArgumentException("The media-folder path cannot contain # or , because oscdimg uses them as boot-data separators.");
        return ["-m", "-o", "-u2", "-udfver102", "-l" + label,
            $"-bootdata:2#p0,e,b{bios}#pEF,e,b{uefi}", media, destination];
    }

    public static string? FindOscdimg()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows Kits", "10",
            "Assessment and Deployment Kit", "Deployment Tools", "amd64", "Oscdimg", "oscdimg.exe");
        return File.Exists(path) ? path : null;
    }

    public static ImageDiscovery DiscoverImages(string root)
    {
        root = DismCommands.SafePath(root);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Source folder does not exist.");
        var pending = new Queue<(string Path, int Depth)>(); pending.Enqueue((root, 0));
        var paths = new List<string>(); var skipped = 0; var visited = 0; var limited = false;
        while (pending.TryDequeue(out var entry))
        {
            if (++visited > 1000 || paths.Count >= 256) { limited = true; break; }
            try
            {
                foreach (var file in Directory.EnumerateFiles(entry.Path))
                {
                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    if (extension is ".wim" or ".esd" or ".vhd" or ".vhdx" || extension == ".swm" && IsFirstSplitWim(file))
                    {
                        paths.Add(file);
                        if (paths.Count >= 256) { limited = true; break; }
                    }
                }
                foreach (var directory in Directory.EnumerateDirectories(entry.Path))
                {
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) { skipped++; continue; }
                    if (entry.Depth >= 4) { limited = true; continue; }
                    if (visited + pending.Count >= 1000) { limited = true; break; }
                    pending.Enqueue((directory, entry.Depth + 1));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { skipped++; }
        }
        return new(paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray(), skipped, limited);
    }

    private static bool IsFirstSplitWim(string path)
    {
        try
        {
            using var input = File.OpenRead(path); var header = new byte[44]; input.ReadExactly(header);
            return header.AsSpan(0, 8).SequenceEqual("MSWIM\0\0\0"u8) &&
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(40)) == 1;
        }
        catch (IOException) { return false; }
    }

    // The script is fixed. Caller-selected paths travel in the child environment,
    // never as PowerShell source text. Mounts not created by this call are refused.
    public const string ImportIsoScript = """
        $ErrorActionPreference = 'Stop'
        $ProgressPreference = 'SilentlyContinue'
        $iso = $env:IMAGEFORGE_ISO_SOURCE
        $destination = $env:IMAGEFORGE_ISO_DESTINATION
        $existing = Get-DiskImage -ImagePath $iso
        if ($existing.Attached) { throw 'This ISO is already mounted. Eject it before importing with ImageForge.' }
        $owned = $false
        try {
            $disk = Mount-DiskImage -ImagePath $iso -StorageType ISO -Access ReadOnly -PassThru
            $owned = $true
            $volumes = @($disk | Get-Volume | Where-Object { $_.DriveLetter })
            if ($volumes.Count -ne 1) { throw 'The ISO must expose exactly one volume with a drive letter.' }
            $root = [string]$volumes[0].DriveLetter + ':\'
            foreach ($item in Get-ChildItem -LiteralPath $root -Force) {
                Write-Output ('Copying ' + $item.Name)
                Copy-Item -LiteralPath $item.FullName -Destination $destination -Recurse -Force
            }
            Get-ChildItem -LiteralPath $destination -Recurse -File -Force | ForEach-Object { $_.IsReadOnly = $false }
            Write-Output ('Media copied to ' + $destination)
        }
        finally {
            if ($owned) { Dismount-DiskImage -ImagePath $iso -ErrorAction Stop }
        }
        """;

    public static async Task ImportIsoAsync(string iso, string destination, Action<string> log)
    {
        iso = DismCommands.ExistingFile(iso, ".iso");
        destination = DismCommands.SafePath(destination);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException("Choose a new, nonexistent destination folder for the ISO import.");
        if (!Directory.Exists(Path.GetDirectoryName(destination))) throw new DirectoryNotFoundException("Select an existing parent folder.");
        ExternalProcess.RequireWindowsAdministrator();
        Directory.CreateDirectory(destination);
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(ImportIsoScript));
        try
        {
            await ExternalProcess.RunAsync(executable, ["-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", encoded], log,
                new Dictionary<string, string> { ["IMAGEFORGE_ISO_SOURCE"] = iso, ["IMAGEFORGE_ISO_DESTINATION"] = destination });
        }
        catch
        {
            log("ISO import did not complete. The output folder was preserved; inspect it before deleting or retrying. If ISO cleanup failed, eject that ISO in Windows.");
            throw;
        }
    }

    public static async Task CreateIsoAsync(string oscdimg, string media, string destination, string label, Action<string> log)
    {
        oscdimg = DismCommands.ExistingFile(oscdimg, ".exe");
        if (!Path.GetFileName(oscdimg).Equals("oscdimg.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select oscdimg.exe from the Microsoft Windows ADK Deployment Tools.");
        await ExternalProcess.RunAsync(oscdimg, IsoArguments(media, destination, label), log);
    }
}
