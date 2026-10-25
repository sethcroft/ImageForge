using System.Text.Json;

namespace ImageForge.Core;

public sealed partial class DismService
{
    private readonly Action<string> log;
    private readonly SemaphoreSlim operation = new(1, 1);
    private readonly string statePath;
    private readonly Dictionary<string, OwnedMount> known = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> verified = new(StringComparer.OrdinalIgnoreCase);
    private sealed record OwnedMount(string MountPath, string ImagePath, int Index, bool Complete);
    public string? ExecutableOverride { get; set; }
    public event Action<string, int>? PhaseChanged;

    public Task<bool> HasMountedSourceUnderAsync(string directory) => Exclusive(async () =>
    {
        directory = DismCommands.SafePath(directory) + Path.DirectorySeparatorChar;
        return (await Mounted()).Any(m => DismCommands.SafePath(m.ImagePath).StartsWith(directory, StringComparison.OrdinalIgnoreCase));
    });

    public Task ExportEditionAsync(string image, int index, string destination) => ExportImageAsync(image, [index], destination);

    public async Task ExportAllEditionsAsync(string image, string destination)
    {
        var editions = await GetEditionsAsync(image);
        await ExportImageAsync(image, editions.Select(e => e.Index).ToArray(), destination);
    }

    private Task ExportImageAsync(string image, int[] indices, string destination) => Exclusive(async () =>
    {
        if (indices.Length == 0) throw new ArgumentException("No image editions were found to export.");
        var extension = Path.GetExtension(destination).ToLowerInvariant();
        if (extension is not (".wim" or ".esd")) throw new ArgumentException("Export to a new WIM or ESD file.");
        destination = ImageTools.NewOutputFile(destination, extension);
        await RequireSourceUnmounted(image);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".imageforge-" + Guid.NewGuid().ToString("N") + ".partial" + extension);
        var arguments = ImageTools.ExportArguments(image, indices[0], temporary).ToList();
        foreach (var (index, position) in indices.Select((index, position) => (index, position)))
        {
            if (index < 1) throw new ArgumentException("Select a positive edition index.");
            PhaseChanged?.Invoke($"Exporting edition {position + 1} of {indices.Length}", index);
            var current = arguments.Select(arg => arg.StartsWith("/SourceIndex:", StringComparison.Ordinal) ? $"/SourceIndex:{index}" : arg).ToArray();
            await Run(current);
        }
        File.Move(temporary, destination, false);
    });

    public Task DeleteEditionAsync(string image, int index) => Exclusive(async () =>
    {
        image = DismCommands.ExistingFile(image, ".wim");
        await RequireSourceUnmounted(image);
        var editions = DismParser.ParseEditions(await Run(["/English", "/Get-WimInfo", $"/WimFile:{image}"]));
        if (editions.Count < 2) throw new InvalidOperationException("Keep at least one edition in the image. Export a new file instead.");
        if (!editions.Any(e => e.Index == index)) throw new ArgumentException("That edition no longer exists in this image.");
        await Run(["/English", "/Delete-Image", $"/ImageFile:{image}", $"/Index:{index}", "/CheckIntegrity"]);
    });

    public Task SplitImageAsync(string image, string destination, int sizeMb) => Exclusive(async () =>
    {
        var args = ImageTools.SplitArguments(image, destination, sizeMb);
        await RequireSourceUnmounted(image);
        await Run(args);
    });

    private async Task RequireSourceUnmounted(string image)
    {
        if ((await Mounted()).Any(m => SamePath(m.ImagePath, image)))
            throw new InvalidOperationException("Save or discard every mount of this source image before exporting or splitting it.");
    }

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
        var image = DismCommands.ExistingFile(imagePath, ".wim", ".esd", ".swm", ".vhd", ".vhdx");
        var isVirtualDisk = Path.GetExtension(image).ToLowerInvariant() is ".vhd" or ".vhdx";
        return DismParser.ParseEditions(await Exclusive(() => Run(isVirtualDisk
            ? ["/English", "/Get-ImageInfo", $"/ImageFile:{image}", "/Index:1"]
            : ["/English", "/Get-WimInfo", $"/WimFile:{image}"])));
    }

    public async Task MountAsync(string imagePath, int index, string mountPath)
    {
        var image = DismCommands.ExistingFile(imagePath, ".wim", ".vhd", ".vhdx");
        var mount = DismCommands.SafePath(mountPath);
        var mountArguments = ImageTools.MountArguments(image, index, mount);
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
                await Run(mountArguments);
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

    public Task<IReadOnlyList<FeatureInfo>> GetFeaturesAsync(string mountPath) => GetFeaturesAsync(ServicingTarget.Mounted(mountPath));

    public Task ApplyStepAsync(string mountPath, PlanStep step) => ApplyStepAsync(ServicingTarget.Mounted(mountPath), step);

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
            if (commit && Path.GetExtension(known[mount].ImagePath).Equals(".wim", StringComparison.OrdinalIgnoreCase)) args.Add("/CheckIntegrity");
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

    private Task<string> Run(IReadOnlyList<string> args)
    {
        ExternalProcess.RequireWindowsAdministrator();
        var executable = string.IsNullOrWhiteSpace(ExecutableOverride)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "Sysnative" : "System32", "dism.exe")
            : DismCommands.ExistingFile(ExecutableOverride, ".exe");
        if (!Path.GetFileName(executable).Equals("dism.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select dism.exe from Windows or the Microsoft Windows ADK.");
        return ExternalProcess.RunAsync(executable, args, log, allowRestartCode: true);
    }
}
