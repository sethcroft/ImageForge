using System.Runtime.InteropServices;
using System.Text;

namespace ImageForge.Core;

public enum ServicingTargetKind { MountedImage, RunningWindows, DeployedVolume }
public sealed record ServicingTarget(ServicingTargetKind Kind, string? Root, string? VolumeIdentity = null)
{
    public static ServicingTarget Mounted(string root) => new(ServicingTargetKind.MountedImage, DismCommands.SafePath(root));
    public static ServicingTarget Running => new(ServicingTargetKind.RunningWindows, null);
    public bool WritesImmediately => Kind != ServicingTargetKind.MountedImage;
    public string DisplayName => Kind switch
    {
        ServicingTargetKind.RunningWindows => "Running Windows on this PC",
        ServicingTargetKind.DeployedVolume => "Windows installation on " + Root,
        _ => "Mounted image at " + Root
    };
}

internal static class DeployedWindows
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(string volumeMountPoint, StringBuilder volumeName, int bufferLength);

    public static string Validate(string root)
    {
        ExternalProcess.RequireWindowsAdministrator();
        root = DismCommands.SafePath(root);
        if (root.StartsWith(@"\\", StringComparison.Ordinal) || !string.Equals(root, Path.GetPathRoot(root), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select the root of a local drive containing Windows, such as D:\\, rather than its Windows subfolder.");
        string Volume(string drive)
        {
            var buffer = new StringBuilder(128);
            if (!GetVolumeNameForVolumeMountPoint(drive, buffer, buffer.Capacity))
                throw new ArgumentException("This drive does not expose a stable local volume identity. Use an assigned local drive letter.");
            return buffer.ToString();
        }
        var identity = Volume(root);
        var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!;
        if (identity.Equals(Volume(systemRoot), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("That is the running Windows volume. Choose This PC (live) explicitly to edit it.");
        foreach (var hive in new[] { "SYSTEM", "SOFTWARE" })
            if (!File.Exists(Path.Combine(root, "Windows", "System32", "config", hive)))
                throw new ArgumentException("The selected drive does not contain a Windows installation at its root.");
        return identity;
    }
}
