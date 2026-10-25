using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace ImageForge.Core;

internal static class ExternalProcess
{
    public sealed record Outcome(int ExitCode, string Output);
    public static void RequireWindowsAdministrator()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Image operations require Windows 10 or 11 and administrator access.");
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            throw new InvalidOperationException("Restart ImageForge as administrator.");
    }

    public static async Task<string> RunAsync(string executable, IReadOnlyList<string> args, Action<string> log,
        IReadOnlyDictionary<string, string>? environment = null, bool allowRestartCode = false)
        => (await ExecuteAsync(executable, args, log, environment, allowRestartCode)).Output;

    public static async Task<Outcome> ExecuteAsync(string executable, IReadOnlyList<string> args, Action<string> log,
        IReadOnlyDictionary<string, string>? environment = null, bool allowRestartCode = false, IReadOnlySet<int>? acceptedCodes = null)
    {
        RequireWindowsAdministrator();
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in args) start.ArgumentList.Add(argument);
        if (environment is not null) foreach (var item in environment) start.Environment[item.Key] = item.Value;
        log(Path.GetFileName(executable) + " " + string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)));
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
        if (process.ExitCode != 0 && !(allowRestartCode && process.ExitCode == 3010) && acceptedCodes?.Contains(process.ExitCode) != true)
            throw new ToolExecutionException(Path.GetFileName(executable), process.ExitCode);
        if (truncated) throw new InvalidOperationException("Tool output exceeded the capture limit. The command may have completed; inspect its output files and logs before retrying.");
        if (process.ExitCode == 3010) log("DISM reports a reboot is required. ImageForge will not restart this computer.");
        return new(process.ExitCode, stdout.ToString());
    }
}
