using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace ImageForge.Core;

public sealed class DismService
{
    private readonly Action<string> log;
    private readonly SemaphoreSlim operation = new(1, 1);
    private readonly string statePath;
    private readonly Dictionary<string, OwnedMount> known = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> verified = new(StringComparer.OrdinalIgnoreCase);
    private sealed record OwnedMount(string MountPath, string ImagePath, int Index, bool Complete);

    public DismService(Action<string> log)
    {
        this.log = log;
        statePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImageForge", "mounts.json");
        if (File.Exists(statePath))
        {
            var records = JsonSerializer.Deserialize<List<OwnedMount>>(File.ReadAllText(statePath))
                ?? throw new InvalidDataException("The ImageForge mount journal is invalid. Preserve it before manual recovery.");
            foreach (var record in records) known.Add(DismCommands.SafePath(record.MountPath), record);
        }
    }

    public bool IsMountedOwned(string mountPath)
    {
        try { return verified.Contains(DismCommands.SafePath(mountPath)); }
        catch (ArgumentException) { return false; }
    }

    public async Task<IReadOnlyList<ImageEdition>> GetEditionsAsync(string imagePath)
    {
        var image = DismCommands.ExistingFile(imagePath, ".wim", ".esd");
        return DismParser.ParseEditions(await Exclusive(() => Run(["/English", "/Get-WimInfo", $"/WimFile:{image}"])));
    }

    public async Task MountAsync(string imagePath, int index, string mountPath)
    {
        var image = DismCommands.ExistingFile(imagePath, ".wim");
        var mount = DismCommands.SafePath(mountPath);
        if (index < 1) throw new ArgumentException("Select a positive image index.");
        if (Path.GetPathRoot(mount) == mount) throw new ArgumentException("Choose an empty mount folder, not a drive root.");
        if (mount.StartsWith(image + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || string.Equals(mount, image, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Mount folder cannot be the source image.");
        await Exclusive(async () =>
        {
            var mounted = await Mounted();
            if (mounted.Any(m => SamePath(m.MountPath, mount))) throw new InvalidOperationException("This folder already contains a mounted image. Use a new empty folder.");
            if (known.TryGetValue(mount, out var previous) && !previous.Complete)
                throw new InvalidOperationException("An interrupted mount reservation exists for this folder. Inspect DISM's mount state and the ImageForge journal before recovery.");
            Directory.CreateDirectory(mount);
            if (Directory.EnumerateFileSystemEntries(mount).Any()) throw new InvalidOperationException("Mount folder must be empty.");
            known[mount] = new(mount, image, index, false); Save();
            try
            {
                await Run(["/English", "/Mount-Image", $"/ImageFile:{image}", $"/Index:{index}", $"/MountDir:{mount}", "/CheckIntegrity"]);
                var actual = (await Mounted()).SingleOrDefault(m => SamePath(m.MountPath, mount));
                if (actual is null || !SamePath(actual.ImagePath, image) || actual.ImageIndex != index || actual.Status != "Ok")
                    throw new InvalidOperationException("DISM mount identity could not be verified. Mount reservation retained for manual recovery.");
                known[mount] = new(mount, image, index, true); Save(); verified.Add(mount);
            }
            catch { verified.Remove(mount); throw; }
        });
    }

    public async Task ReconcileOwnedMountAsync(string mountPath)
    {
        var mount = DismCommands.SafePath(mountPath);
        await Exclusive(() => Reconcile(mount));
    }

    private async Task Reconcile(string mount)
    {
        verified.Remove(mount);
        if (!known.TryGetValue(mount, out var record) || !record.Complete)
            throw new InvalidOperationException("This mount is not recorded as owned by ImageForge. Mount an image through this application first.");
        var actual = (await Mounted()).SingleOrDefault(m => SamePath(m.MountPath, mount));
        if (actual is null) throw new InvalidOperationException("The recorded image is no longer mounted. Choose a new empty mount folder or remount it.");
        if (!SamePath(actual.ImagePath, record.ImagePath) || actual.ImageIndex != record.Index || actual.Status != "Ok")
            throw new InvalidOperationException("The mounted image identity or status differs from ImageForge's journal. No changes were made.");
        verified.Add(mount);
    }

    public async Task<IReadOnlyList<FeatureInfo>> GetFeaturesAsync(string mountPath)
    {
        var mount = DismCommands.SafePath(mountPath);
        return await Exclusive(async () =>
        {
            await Reconcile(mount);
            return DismParser.ParseFeatures(await Run(["/English", $"/Image:{mount}", "/Get-Features", "/Format:List"]));
        });
    }

    public async Task ApplyStepAsync(string mountPath, PlanStep step)
    {
        var mount = DismCommands.SafePath(mountPath);
        var args = DismCommands.ForStep(mount, step);
        await Exclusive(async () => { await Reconcile(mount); await Run(args); });
    }

    public async Task ApplyAsync(string mountPath, IEnumerable<PlanStep> steps)
    {
        var plan = steps.ToList();
        // Validate the entire plan before making the first change.
        foreach (var step in plan) DismCommands.ForStep(mountPath, step);
        foreach (var step in plan) await ApplyStepAsync(mountPath, step);
    }

    public async Task UnmountAsync(string mountPath, bool commit)
    {
        var mount = DismCommands.SafePath(mountPath);
        await Exclusive(async () =>
        {
            await Reconcile(mount);
            var args = new List<string> { "/English", "/Unmount-Image", $"/MountDir:{mount}", commit ? "/Commit" : "/Discard" };
            if (commit) args.Add("/CheckIntegrity");
            await Run(args);
            verified.Remove(mount);
            if ((await Mounted()).Any(m => SamePath(m.MountPath, mount)))
                throw new InvalidOperationException("DISM returned but the image remains mounted. The ownership record was preserved.");
            known.Remove(mount); Save();
        });
    }

    private async Task<IReadOnlyList<MountedImage>> Mounted() => DismParser.ParseMountedImages(await Run(["/English", "/Get-MountedImageInfo"]));

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        var temporary = statePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(known.Values, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, statePath, true);
    }

    private static bool SamePath(string left, string right) => string.Equals(DismCommands.SafePath(left), DismCommands.SafePath(right), StringComparison.OrdinalIgnoreCase);

    private async Task<T> Exclusive<T>(Func<Task<T>> task)
    {
        await operation.WaitAsync();
        try { return await task(); } finally { operation.Release(); }
    }
    private async Task Exclusive(Func<Task> task)
    {
        await operation.WaitAsync();
        try { await task(); } finally { operation.Release(); }
    }

    private async Task<string> Run(IReadOnlyList<string> args)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows image servicing requires Windows 10 or 11, running as administrator.");
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) throw new InvalidOperationException("Restart ImageForge as administrator to use DISM.");
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "Sysnative" : "System32", "dism.exe");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        log("DISM " + string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)));
        using var process = new Process { StartInfo = start };
        process.Start();
        var stdout = new StringBuilder(); var stderr = new StringBuilder();
        var truncated = false;
        async Task Drain(StreamReader reader, StringBuilder capture)
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                log(line);
                if (capture.Length + line.Length > 16 * 1024 * 1024) { truncated = true; continue; }
                capture.AppendLine(line);
            }
        }
        await Task.WhenAll(Drain(process.StandardOutput, stdout), Drain(process.StandardError, stderr), process.WaitForExitAsync());
        if (process.ExitCode is not 0 and not 3010) throw new InvalidOperationException($"DISM failed with exit code {process.ExitCode}.\n{stderr}\n{stdout.ToString()[Math.Max(0, stdout.Length - 2500)..]}");
        if (truncated) throw new InvalidOperationException("DISM output exceeded the capture limit. The command may have completed; inspect DISM's log and mount state before retrying.");
        if (process.ExitCode == 3010) log("DISM reports a reboot is required. ImageForge will not restart this computer.");
        return stdout.ToString();
    }
}
