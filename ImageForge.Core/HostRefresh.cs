using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ImageForge.Core;

public sealed record SetupCompatibility(uint Code, bool CanProceed, string Message)
{
    public static SetupCompatibility FromExitCode(int code) => unchecked((uint)code) switch
    {
        0xC1900210 => new(0xC1900210, true, "Compatibility scan passed. Windows Setup will still review the available upgrade and preservation choices."),
        0xC1900208 => new(0xC1900208, false, "An application or driver blocks this upgrade. Review Windows Setup's compatibility report."),
        0xC1900204 => new(0xC1900204, false, "This media cannot preserve the requested applications and settings on this PC."),
        0xC1900200 => new(0xC1900200, false, "This PC does not meet the target Windows requirements."),
        0xC190020E => new(0xC190020E, false, "There is not enough free disk space for this upgrade."),
        0xC190010E => new(0xC190010E, false, "Windows Setup requires acceptance of its license terms. ImageForge does not accept them automatically."),
        _ => new(unchecked((uint)code), false, $"Windows Setup did not report a completed compatibility pass (0x{unchecked((uint)code):X8}). Review its result before trying again.")
    };
}

public static class HostRefresh
{
    public static string SetupPath(string mediaRoot)
    {
        mediaRoot = DismCommands.SafePath(mediaRoot);
        var setup = DismCommands.ExistingFile(Path.Combine(mediaRoot, "setup.exe"), ".exe");
        if (!Directory.Exists(Path.Combine(mediaRoot, "sources")) ||
            !new[] { "install.wim", "install.esd" }.Any(name => File.Exists(Path.Combine(mediaRoot, "sources", name))))
            throw new ArgumentException("Choose original Microsoft installation media containing setup.exe and sources/install.wim or install.esd.");
        return setup;
    }
    private static async Task VerifySetupAsync(string setup, Action<string> log)
    {
        ExternalProcess.RequireWindowsAdministrator();
        const string script = "$ErrorActionPreference='Stop'; $s=Get-AuthenticodeSignature -LiteralPath $env:IMAGEFORGE_SETUP_FILE; @{Status=[string]$s.Status;Subject=[string]$s.SignerCertificate.Subject}|ConvertTo-Json -Compress";
        var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var output = await ExternalProcess.RunAsync(powershell, ["-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script))], log,
            new Dictionary<string, string> { ["IMAGEFORGE_SETUP_FILE"] = setup });
        using var json = JsonDocument.Parse(output);
        if (json.RootElement.GetProperty("Status").GetString() != "Valid" ||
            json.RootElement.GetProperty("Subject").GetString() is not { } subject ||
            !(subject.Contains("CN=Microsoft Windows", StringComparison.OrdinalIgnoreCase) || subject.Contains("CN=Microsoft Corporation", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Windows could not verify a valid Microsoft signature on this setup.exe. Use original Microsoft installation media.");
    }
    public static IReadOnlyList<string> CompatibilityArguments => ["/Auto", "Upgrade", "/Compat", "ScanOnly"];
    public static async Task<SetupCompatibility> ScanAsync(string mediaRoot, Action<string> log)
    {
        var setup = SetupPath(mediaRoot); await VerifySetupAsync(setup, log);
        var codes = new HashSet<int>(new uint[] { 0xC1900210, 0xC1900208, 0xC1900204, 0xC1900200, 0xC190020E, 0xC190010E }.Select(value => unchecked((int)value)));
        var result = await ExternalProcess.ExecuteAsync(setup, CompatibilityArguments, log, acceptedCodes: codes);
        return SetupCompatibility.FromExitCode(result.ExitCode);
    }
    public static async Task LaunchInteractiveAsync(string mediaRoot, Action<string> log)
    {
        var setup = SetupPath(mediaRoot); await VerifySetupAsync(setup, log);
        // No unattended, quiet, license-acceptance or compatibility-bypass flags.
        _ = Process.Start(new ProcessStartInfo(setup) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(setup)! })
            ?? throw new InvalidOperationException("Windows Setup could not be started.");
    }
}
