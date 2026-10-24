namespace ImageForge.Core;

public sealed record ImageEdition(int Index, string Name, string Description);
public sealed record FeatureInfo(string Name, string State);
public sealed record PlanStep(string Kind, string Value)
{
    public override string ToString() => $"{Kind}: {Value}";
}
public sealed record ProjectPreset(string ImagePath, int ImageIndex, string MountPath, List<PlanStep> Steps);
public sealed record MountedImage(string MountPath, string ImagePath, int ImageIndex, string Status);

public static class DismParser
{
    public static IReadOnlyList<ImageEdition> ParseEditions(string output)
    {
        var result = new List<ImageEdition>();
        int index = 0; string name = "", description = "";
        void Add() { if (index > 0) result.Add(new(index, name, description)); }
        foreach (var (key, value) in Fields(output))
        {
            if (key == "Index") { Add(); index = int.TryParse(value, out var n) ? n : 0; name = description = ""; }
            else if (key == "Name") name = value;
            else if (key == "Description") description = value;
        }
        Add(); return result;
    }

    public static IReadOnlyList<FeatureInfo> ParseFeatures(string output)
    {
        var result = new List<FeatureInfo>(); string? name = null;
        foreach (var (key, value) in Fields(output))
        {
            if (key == "Feature Name") name = value;
            else if (key == "State" && name is not null) { result.Add(new(name, value)); name = null; }
        }
        return result;
    }

    public static IReadOnlyList<MountedImage> ParseMountedImages(string output)
    {
        var result = new List<MountedImage>();
        string mount = "", image = "", status = ""; int index = 0;
        void Add() { if (mount.Length > 0 && image.Length > 0 && index > 0) result.Add(new(mount, image, index, status)); }
        foreach (var (key, value) in Fields(output))
        {
            if (key == "Mount Dir") { Add(); mount = value; image = status = ""; index = 0; }
            else if (key == "Image File") image = value;
            else if (key == "Image Index") index = int.TryParse(value, out var n) ? n : 0;
            else if (key == "Status") status = value;
        }
        Add(); return result;
    }

    private static IEnumerable<(string Key, string Value)> Fields(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var separator = line.IndexOf(':');
            if (separator >= 0) yield return (line[..separator].Trim(), line[(separator + 1)..].Trim());
        }
    }
}
