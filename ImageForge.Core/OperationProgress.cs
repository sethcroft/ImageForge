using System.Globalization;
using System.Text.RegularExpressions;

namespace ImageForge.Core;

public static class OperationProgress
{
    public static double? Percentage(string line)
    {
        var progressLine = Regex.IsMatch(line, @"^\s*\[[=\s\d.%]+\]", RegexOptions.CultureInvariant) ||
            Regex.IsMatch(line, @"^\s*\(?\d{1,3}(?:\.\d+)?%\s+complete", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!progressLine) return null;
        var match = Regex.Match(line, @"(?<![\d.])(\d{1,3}(?:\.\d+)?)\s*%", RegexOptions.CultureInvariant);
        return match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var value) && value is >= 0 and <= 100 ? value : null;
    }
}

public sealed class ToolExecutionException(string tool, int exitCode)
    : Exception($"{tool} could not complete the operation (error 0x{unchecked((uint)exitCode):X8}). See the diagnostics file for details.")
{
    public int ExitCode { get; } = exitCode;
}
