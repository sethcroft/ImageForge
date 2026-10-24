namespace ImageForge.Core;

public static class DismCommands
{
    public static string ExistingFile(string path, params string[] extensions)
    {
        var full = SafePath(path);
        if (!File.Exists(full)) throw new FileNotFoundException("File does not exist.", full);
        if (extensions.Length > 0 && !extensions.Contains(Path.GetExtension(full), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Expected {string.Join(" or ", extensions)} file.");
        return full;
    }

    public static string SafePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('"') || path.Any(char.IsControl))
            throw new ArgumentException("A valid absolute path is required.");
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Path must be absolute.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    public static string FeatureName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw new ArgumentException("Select a valid Windows feature name.");
        return name;
    }

    // Each returned value is one process argument, never interpreted by a shell.
    public static IReadOnlyList<string> ForStep(string mountPath, PlanStep step)
    {
        var args = new List<string> { "/English", $"/Image:{SafePath(mountPath)}" };
        switch (step.Kind)
        {
            case "add-driver":
                var driver = SafePath(step.Value);
                args.Add("/Add-Driver");
                if (Directory.Exists(driver)) { args.Add($"/Driver:{driver}"); args.Add("/Recurse"); }
                else args.Add($"/Driver:{ExistingFile(driver, ".inf")}");
                break;
            case "add-package":
                args.Add("/Add-Package"); args.Add($"/PackagePath:{ExistingFile(step.Value, ".cab", ".msu")}"); break;
            case "enable-feature":
                args.Add("/Enable-Feature"); args.Add($"/FeatureName:{FeatureName(step.Value)}"); args.Add("/All"); args.Add("/LimitAccess"); break;
            case "disable-feature":
                args.Add("/Disable-Feature"); args.Add($"/FeatureName:{FeatureName(step.Value)}"); break;
            default: throw new ArgumentException($"Unsupported plan action: {step.Kind}");
        }
        args.Add("/NoRestart"); return args;
    }
}
