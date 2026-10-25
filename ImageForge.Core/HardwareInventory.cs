using System.Text;
using System.Text.Json;

namespace ImageForge.Core;

public static class HardwareInventory
{
    public static async Task<string> ReadJsonAsync()
    {
        const string script = "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); $ErrorActionPreference='Stop'; $devices=@(Get-CimInstance Win32_PnPEntity -OperationTimeoutSec 30 | Sort-Object PNPClass,Name | Select-Object Name,PNPClass,Manufacturer,Status,ConfigManagerErrorCode); ConvertTo-Json -InputObject $devices -Depth 3 -Compress";
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        var json = await ExternalProcess.RunAsync(executable, ["-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script))], _ => { });
        using var doc = JsonDocument.Parse(json.TrimStart('\uFEFF'));
        if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Windows did not return a hardware list.");
        return JsonSerializer.Serialize(new { format = "ImageForge.Hardware.v1", createdUtc = DateTime.UtcNow, devices = doc.RootElement }, new JsonSerializerOptions { WriteIndented = true });
    }
}
