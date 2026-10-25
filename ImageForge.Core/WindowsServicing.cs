namespace ImageForge.Core;

public sealed partial class DismService
{
    public ServicingTarget SelectDeployedVolume(string root)
    {
        root = DismCommands.SafePath(root);
        return new(ServicingTargetKind.DeployedVolume, root, DeployedWindows.Validate(root));
    }
    private async Task ValidateTarget(ServicingTarget target)
    {
        if (target.Kind == ServicingTargetKind.MountedImage) await Reconcile(target.Root!);
        else if (target.Kind == ServicingTargetKind.DeployedVolume)
        {
            if (string.IsNullOrWhiteSpace(target.VolumeIdentity) || !DeployedWindows.Validate(target.Root!).Equals(target.VolumeIdentity, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The selected Windows drive has changed. Select the intended installation again.");
        }
        else if (target.Kind != ServicingTargetKind.RunningWindows) throw new ArgumentException("Unknown Windows target.");
    }
    public Task<IReadOnlyList<FeatureInfo>> GetFeaturesAsync(ServicingTarget target) => Exclusive(async () =>
    {
        await ValidateTarget(target);
        return DismParser.ParseFeatures(await Run([.. DismCommands.TargetPrefix(target), "/Get-Features", "/Format:List"]));
    });
    public static IReadOnlyList<string> ParseTargetEditions(string output) => output.Split('\n')
        .Select(line => line.Split(':', 2)).Where(fields => fields.Length == 2 && fields[0].Trim() == "Target Edition")
        .Select(fields => fields[1].Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public Task<IReadOnlyList<string>> GetTargetEditionsAsync(ServicingTarget target) => Exclusive(async () =>
    {
        if (target.Kind == ServicingTargetKind.RunningWindows) throw new ArgumentException("Select an offline image for edition conversion.");
        await ValidateTarget(target);
        return ParseTargetEditions(await Run([.. DismCommands.TargetPrefix(target), "/Get-TargetEditions"]));
    });
    public Task ApplyStepAsync(ServicingTarget target, PlanStep step) => Exclusive(async () =>
    {
        var args = DismCommands.ForTarget(target, step);
        await ValidateTarget(target);
        if (target.Kind == ServicingTargetKind.RunningWindows && step.Kind == "add-package" &&
            Path.GetExtension(step.Value).Equals(".msu", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            throw new ArgumentException("Online MSU integration requires Windows 11 or newer. Use an applicable CAB or service an offline image on older hosts.");
        if (step.Kind == "set-edition")
        {
            var editions = ParseTargetEditions(await Run([.. DismCommands.TargetPrefix(target), "/Get-TargetEditions"]));
            if (!editions.Contains(step.Value, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException("The selected edition is no longer an eligible upgrade for this image.");
        }
        await Run(args);
    });
}
