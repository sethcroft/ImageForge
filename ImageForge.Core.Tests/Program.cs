using ImageForge.Core;
using System.Xml.Linq;

var count = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAILED: " + message);
    Console.WriteLine("PASS " + message); count++;
}
void Reject(Action action, string message)
{
    try { action(); } catch (ArgumentException) { Check(true, message); return; }
    throw new Exception("FAILED (accepted invalid input): " + message);
}

var editions = DismParser.ParseEditions("Index : 1\r\nName : Windows 11 Home\r\nDescription : Home: desktop\r\nSize : 2\r\n\r\nIndex : 6\r\nName : Windows 11 Pro\r\nDescription : Pro\r\n");
Check(editions.Count == 2 && editions[0].Index == 1 && editions[1].Index == 6 && editions[0].Description == "Home: desktop", "edition blocks retain index and colons inside values");
Check(DismParser.ParseEditions("Index : invalid\nName : Ignore").Count == 0, "invalid edition indices are excluded");
var features = DismParser.ParseFeatures("Feature Name : NetFx3\nState : Disabled with Payload Removed\n\nFeature Name : Printing-XPSServices-Features\nState : Enabled\n");
Check(features.Count == 2 && features[0].State == "Disabled with Payload Removed", "feature parser preserves payload and pending state vocabulary");
var mounts = DismParser.ParseMountedImages("Mount Dir : C:\\Images\\Mount\nImage File : D:\\sources\\install.wim\nImage Index : 6\nMounted Read/Write : Yes\nStatus : Ok\n\nMount Dir : C:\\Other\nImage File : D:\\other.wim\nImage Index : 1\nStatus : Needs Remount\n");
Check(mounts.Count == 2 && mounts[0].ImagePath == @"D:\sources\install.wim" && mounts[1].Status == "Needs Remount", "mount parser retains complete source identity and status");
Check(DismParser.ParseMountedImages("Mount Dir : X\nStatus : Ok").Count == 0, "partial mount records never establish identity");

var temp = Path.Combine(Path.GetTempPath(), "imageforge-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var driver = Path.Combine(temp, "driver & unusual name.inf"); File.WriteAllText(driver, "[Version]");
    var argsList = DismCommands.ForStep(temp, new("add-driver", driver));
    Check(argsList.Contains("/Driver:" + driver) && argsList.Count == 5, "driver path with spaces and shell metacharacters is a single argument");
    Check(argsList.All(a => !a.Equals("/Online", StringComparison.OrdinalIgnoreCase)), "servicing commands exclusively target offline images");
    Check(DismCommands.ForStep(temp, new("add-driver", temp)).Contains("/Recurse"), "driver directory explicitly requests recursive INF import");
    var enable = DismCommands.ForStep(temp, new("enable-feature", "NetFx3"));
    Check(enable.Contains("/LimitAccess") && enable.Contains("/All"), "feature enabling stays offline and includes parent features");
    Check(!DismCommands.ForStep(temp, new("disable-feature", "NetFx3")).Contains("/Remove"), "feature disabling retains payload");
    Reject(() => DismCommands.ForStep(temp, new("enable-feature", "NetFx3 /Online")), "feature arguments cannot inject DISM switches");
    Reject(() => DismCommands.ForStep(temp, new("arbitrary-command", "value")), "unknown preset action is rejected");
    Reject(() => DismCommands.SafePath("relative/path"), "relative filesystem paths are rejected");
    Reject(() => DismCommands.SafePath(temp + "\"/Online"), "quoted path injection is rejected");
    Reject(() => DismCommands.ForStep(temp, new("add-package", driver)), "update packages require CAB or MSU extension");
}
finally { Directory.Delete(temp, true); }

var xml = XDocument.Parse(UnattendWriter.Generate("WORKSTATION-1", "en-US", "Pacific Standard Time"));
XNamespace ns = "urn:schemas-microsoft-com:unattend";
Check(xml.Root?.Name == ns + "unattend", "answer file has the Microsoft unattend namespace");
Check(xml.Descendants(ns + "settings").Select(e => (string?)e.Attribute("pass")).SequenceEqual(new[] { "specialize", "oobeSystem" }), "answer file uses valid setup passes");
Check(xml.Descendants(ns + "component").All(e => (string?)e.Attribute("processorArchitecture") == "amd64" && (string?)e.Attribute("publicKeyToken") == "31bf3856ad364e35"), "answer file components target x64 Windows");
Check(!xml.Descendants().Any(e => e.Name.LocalName.Contains("Password") || e.Name.LocalName.Contains("ProductKey")), "answer file carries no credentials or licensing bypass");
Reject(() => UnattendWriter.Generate("1234", "en-US", "UTC"), "all-numeric computer names are rejected");
Reject(() => UnattendWriter.Generate("PC", "not-a-locale", "UTC"), "invalid locale syntax is rejected");
Console.WriteLine($"{count} checks passed. No Windows servicing commands were executed.");
