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

var editingPages = new[] { 1, 2, 3, 4, 6, 10, 11, 12, 13, 14 };
Check(editingPages.All(page => ImageForge.App.WorkspaceNavigation.ResolvePage(page, false) == 0), "editing navigation remains at Image before any target is loaded, including preset review");
Check(editingPages.All(page => ImageForge.App.WorkspaceNavigation.ResolvePage(page, true) == page), "loaded image, reconnected mount and deployed targets unlock the editing pages");
Check(new[] { 0, 5, 7, 8, 9 }.All(page => ImageForge.App.WorkspaceNavigation.ResolvePage(page, false) == page), "image tools, compatibility, downloads and host refresh stay reachable before loading an image");
Check(ImageForge.App.WorkspaceNavigation.ResolvePage(6, false) == 0 && ImageForge.App.WorkspaceNavigation.ResolvePage(6, true) == 6, "unloading a target returns Apply to Image without restricting a loaded session");
Check(new[] { -1, 15, 100 }.All(page => ImageForge.App.WorkspaceNavigation.ResolvePage(page, true) == 0), "invalid navigation destinations resolve to Image");

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
    Check(argsList.All(a => !a.Equals("/Online", StringComparison.OrdinalIgnoreCase)), "default image commands exclusively target offline images");
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

var mediaTests = Path.Combine(Path.GetTempPath(), "imageforge-media-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(mediaTests);
try
{
    var source = Path.Combine(mediaTests, "source & test.wim"); File.WriteAllText(source, "fixture");
    var wimOutput = Path.Combine(mediaTests, "export.wim");
    var export = ImageTools.ExportArguments(source, 6, wimOutput);
    Check(export.Contains("/SourceIndex:6") && export.Contains("/Compress:max"), "WIM export preserves selected source edition and lossless compression choice");
    Check(ImageTools.ExportArguments(source, 1, Path.Combine(mediaTests, "export.esd")).Contains("/Compress:recovery"), "ESD export selects recovery compression");
    Reject(() => ImageTools.ExportArguments(source, 0, wimOutput), "export rejects invalid source index before tool execution");
    var overwriteRejected = false;
    try { ImageTools.ExportArguments(source, 1, source); } catch (IOException) { overwriteRejected = true; }
    Check(overwriteRejected, "image export cannot overwrite the original input file");
    var split = ImageTools.SplitArguments(source, Path.Combine(mediaTests, "install.swm"), 3800);
    Check(split.Contains("/FileSize:3800") && split.Contains("/CheckIntegrity"), "split output requests FAT32-friendly part size and integrity checks");
    Reject(() => ImageTools.SplitArguments(source, Path.Combine(mediaTests, "install.swm"), 0), "invalid split size is rejected before writing");
    File.WriteAllText(Path.Combine(mediaTests, "install2.swm"), "old part");
    var collidingPartsRejected = false;
    try { ImageTools.SplitArguments(source, Path.Combine(mediaTests, "install.swm"), 3800); } catch (IOException) { collidingPartsRejected = true; }
    Check(collidingPartsRejected, "split-set collisions are rejected even when the first part does not exist");
    var media = Path.Combine(mediaTests, "Windows media & example"); Directory.CreateDirectory(media);
    foreach (var relative in new[] { "boot/etfsboot.com", "efi/microsoft/boot/efisys.bin", "sources/boot.wim", "sources/install.wim", "setup.exe" })
    {
        var file = Path.Combine(media, relative); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, "fixture");
    }
    var iso = ImageTools.IsoArguments(media, Path.Combine(mediaTests, "out.iso"), "IMAGEFORGE");
    Check(iso.Count(a => a.StartsWith("-bootdata:")) == 1 && iso.Single(a => a.StartsWith("-bootdata:")).Contains("#pEF,e,b"), "ISO builds include both BIOS and UEFI entries in one argument");
    Check(iso[^2] == media && iso.Contains("-u2"), "media paths with spaces and metacharacters remain a single argument");
    Reject(() => ImageTools.IsoArguments(media, Path.Combine(media, "out.iso"), "IMAGEFORGE"), "ISO output is refused inside its input tree");
    Reject(() => ImageTools.IsoArguments(media, Path.Combine(mediaTests, "out.iso"), "BAD LABEL#"), "malformed ISO labels cannot inject tool options");
    File.Delete(Path.Combine(media, "setup.exe"));
    var incompleteRejected = false;
    try { ImageTools.IsoArguments(media, Path.Combine(mediaTests, "out.iso"), "IMAGEFORGE"); } catch (FileNotFoundException) { incompleteRejected = true; }
    Check(incompleteRejected, "bootable ISO creation refuses incomplete Windows installation media");
    var discovered = ImageTools.DiscoverImages(mediaTests);
    Check(discovered.Paths.Contains(source) && discovered.Paths.Any(p => p.EndsWith("boot.wim")) && discovered.Paths.All(p => Path.GetExtension(p) is ".wim" or ".esd"), "folder discovery inventories image files and excludes update or split-set files");
    var isoFixture = Path.Combine(mediaTests, "source.iso"); File.WriteAllText(isoFixture, "fixture");
    var importOverwriteRejected = false;
    try { ImageTools.ImportIsoAsync(isoFixture, media, _ => { }).GetAwaiter().GetResult(); } catch (IOException) { importOverwriteRejected = true; }
    Check(importOverwriteRejected && File.ReadAllText(Path.Combine(media, "sources", "boot.wim")) == "fixture", "ISO import rejects an existing destination before starting a process or modifying files");
}
finally { Directory.Delete(mediaTests, true); }

Check(OperationProgress.Percentage("[==== 42.5%    ]") == 42.5, "DISM percentages become structured progress");
Check(OperationProgress.Percentage(" 87% complete") == 87, "ISO progress is recognized without displaying tool text");
Check(OperationProgress.Percentage(@"/Image:C:\media 42%\mount") is null, "paths containing percent signs never become progress");
Check(OperationProgress.Percentage("[==== 101%]") is null, "out-of-range tool progress is rejected");
Check(DismCommands.ForTarget(ServicingTarget.Running, new("enable-feature", "NetFx3")).Contains("/Online"), "running Windows is an explicit servicing target");
Reject(() => DismCommands.ForTarget(ServicingTarget.Running, new("add-driver", "unused")), "live target refuses offline-only driver integration before filesystem access");
Reject(() => DismCommands.ForTarget(ServicingTarget.Running, new("set-edition", "Professional")), "client edition upgrade cannot be queued as online servicing");
Reject(() => DismCommands.TargetPrefix(new((ServicingTargetKind)99, null)), "unknown target kinds fail closed");
Check(DismService.ParseTargetEditions("Target Edition : Professional\nTarget Edition : Enterprise\nOther : Ignore").SequenceEqual(new[] { "Professional", "Enterprise" }), "edition choices come from DISM's eligible target records");
Check(SetupCompatibility.FromExitCode(unchecked((int)0xC1900210)).CanProceed, "only completed compatibility scan code authorizes the next wizard step");
Check(!SetupCompatibility.FromExitCode(unchecked((int)0xC1900204)).CanProceed && !SetupCompatibility.FromExitCode(0).CanProceed, "blocked migration and generic success cannot masquerade as a passed upgrade scan");
Check(!HostRefresh.CompatibilityArguments.Contains("/Quiet") && !HostRefresh.CompatibilityArguments.Contains("/EULA"), "compatibility scan preserves Setup interaction and does not accept a license");
Reject(() => MicrosoftMediaDownload.ValidateUri("https://software-download.microsoft.com.evil.example/test.iso"), "lookalike Microsoft domains are rejected");
Reject(() => MicrosoftMediaDownload.ValidateUri("http://software-download.microsoft.com/test.iso"), "media download requires HTTPS");
Reject(() => MicrosoftMediaDownload.ValidateUri("https://user:password@software-download.microsoft.com/test.iso"), "credential-bearing download URLs are rejected");
Reject(() => MicrosoftMediaDownload.ValidateSha256("not-a-hash"), "missing or malformed Microsoft checksum is rejected");

var workflowRoot = Path.Combine(Path.GetTempPath(), "imageforge-workflows-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workflowRoot);
try
{
    var vhd = Path.Combine(workflowRoot, "disk.vhdx"); File.WriteAllText(vhd, "fixture");
    var mountArgs = ImageTools.MountArguments(vhd, 1, Path.Combine(workflowRoot, "mount"));
    Check(!mountArgs.Contains("/CheckIntegrity"), "VHD mount excludes WIM-only integrity options");
    Reject(() => ImageTools.MountArguments(vhd, 2, Path.Combine(workflowRoot, "mount")), "virtual-disk mount rejects indexes other than one");
    void SplitPart(string path, ushort index, ushort total, byte identity)
    {
        var bytes = new byte[44]; "MSWIM\0\0\0"u8.CopyTo(bytes); bytes[24] = identity;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(40), index);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), total); File.WriteAllBytes(path, bytes);
    }
    var first = Path.Combine(workflowRoot, "install.swm"); var second = Path.Combine(workflowRoot, "install2.swm");
    SplitPart(first, 1, 2, 7); SplitPart(second, 2, 2, 7);
    Check(ImageTools.ExportArguments(first, 1, Path.Combine(workflowRoot, "joined.wim")).Contains("/SWMFile:" + Path.Combine(workflowRoot, "install*.swm")), "complete split sets carry a resource reference when joined by export");
    File.Delete(second); var missingRejected = false;
    try { ImageTools.SplitWimPattern(first); } catch (InvalidDataException) { missingRejected = true; }
    Check(missingRejected, "incomplete split sets are refused before export");
    SplitPart(second, 2, 2, 8); var mixedRejected = false;
    try { ImageTools.SplitWimPattern(first); } catch (InvalidDataException) { mixedRejected = true; }
    Check(mixedRejected, "mixed split-set identities are refused");

    var media = Path.Combine(workflowRoot, "template"); Directory.CreateDirectory(media);
    foreach (var relative in new[] { "setup.exe", "sources/boot.wim", "boot/etfsboot.com", "efi/microsoft/boot/efisys.bin", "sources/install.esd", "support/info.txt" })
    {
        var file = Path.Combine(media, relative); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, relative);
    }
    var copied = Path.Combine(workflowRoot, "new-media"); await MediaTemplate.CopyAsync(media, copied);
    Check(File.Exists(Path.Combine(copied, "sources", "boot.wim")) && !File.Exists(Path.Combine(copied, "sources", "install.esd")), "conversion template retains setup files while excluding the old installation image");
    var reuseRejected = false; try { await MediaTemplate.CopyAsync(media, copied); } catch (ArgumentException) { reuseRejected = true; }
    Check(reuseRejected && File.ReadAllText(Path.Combine(copied, "setup.exe")) == "setup.exe", "template copying refuses reused output without changing its files");

    var payload = new byte[35000]; "CD001"u8.CopyTo(payload.AsSpan(16 * 2048 + 1));
    var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload));
    var download = Path.Combine(workflowRoot, "verified.iso");
    using (var client = new MicrosoftMediaDownload(new MediaHandler(_ => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.ByteArrayContent(payload) })))
    { await client.DownloadAsync("https://software-download.microsoft.com/test.iso", digest, download); }
    Check(File.ReadAllBytes(download).SequenceEqual(payload), "download publishes exactly the SHA-256-verified ISO bytes");
    var mismatch = Path.Combine(workflowRoot, "mismatch.iso");
    using (var client = new MicrosoftMediaDownload(new MediaHandler(_ => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.ByteArrayContent(payload) })))
    {
        var rejected = false;
        try { await client.DownloadAsync("https://software-download.microsoft.com/test.iso", new string('0', 64), mismatch); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && !File.Exists(mismatch) && !Directory.EnumerateFiles(workflowRoot, "*.partial").Any(), "checksum mismatch leaves neither a published image nor partial download");
    }
    using (var client = new MicrosoftMediaDownload(new MediaHandler(_ => { var response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Redirect); response.Headers.Location = new Uri("https://evil.example/test.iso"); return response; })))
    {
        var redirectRejected = false;
        try { await client.DownloadAsync("https://software-download.microsoft.com/test.iso", digest, Path.Combine(workflowRoot, "redirect.iso")); } catch (ArgumentException) { redirectRejected = true; }
        Check(redirectRejected, "download redirect cannot escape the Microsoft host allowlist");
    }
    using (var client = new MicrosoftMediaDownload(new MediaHandler(_ => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.ByteArrayContent(payload) })))
    {
        var cancelled = new CancellationTokenSource(); cancelled.Cancel(); var cancellationObserved = false;
        try { await client.DownloadAsync("https://software-download.microsoft.com/test.iso", digest, Path.Combine(workflowRoot, "cancelled.iso"), cancellationToken: cancelled.Token); } catch (OperationCanceledException) { cancellationObserved = true; }
        Check(cancellationObserved && !File.Exists(Path.Combine(workflowRoot, "cancelled.iso")), "cancelled download never publishes a media file");
    }
}
finally { Directory.Delete(workflowRoot, true); }

var stableVersion = ToolRelease.Parse("v0.5.0")!;
var previewVersion = ToolRelease.Parse("v0.5.0-preview.10")!;
Check(stableVersion.CompareTo(previewVersion) > 0, "stable releases sort after previews of the same version");
Check(previewVersion.CompareTo(ToolRelease.Parse("v0.5.0-preview.2")) > 0, "preview version numbers compare numerically");
Check(new[] { "v0.5", "v0.5.0/../../evil", "https://evil.example", "v0.5.0-preview.-1" }.All(tag => ToolRelease.Parse(tag) is null), "release tags reject invalid versions and injected URL paths");
var catalog = ReleaseCatalog.Parse("""
[{"draft":false,"prerelease":true,"tag_name":"v0.4.0-preview.1"},
 {"draft":false,"prerelease":true,"tag_name":"v0.5.0-preview.2"},
 {"draft":false,"prerelease":false,"tag_name":"v0.4.0"},
 {"draft":true,"prerelease":false,"tag_name":"v9.0.0"},
 {"draft":false,"prerelease":false,"tag_name":"v8.0.0-preview.1"},
 {"draft":false,"prerelease":false,"tag_name":"v0.5.0/evil"}]
""");
Check(catalog.Stable?.Tag == "v0.4.0" && catalog.Preview?.Tag == "v0.5.0-preview.2", "catalog selects highest valid stable and preview while ignoring drafts and inconsistent tags");
Check(catalog.Preview!.Page.AbsoluteUri == "https://github.com/sethcroft/ImageForge/releases/tag/v0.5.0-preview.2", "release links are constructed under the fixed ImageForge repository");
Check(ReleaseCatalog.Parse("[]") is { Stable: null, Preview: null }, "empty release catalogs report no versions rather than fabricating one");
var supportRoot = Path.Combine(Path.GetTempPath(), "imageforge-support-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(supportRoot);
try
{
    var log = Path.Combine(supportRoot, "session.log"); File.WriteAllText(log, "test diagnostic fixture");
    var preset = Path.Combine(supportRoot, "private-filename.json"); File.WriteAllText(preset, "{}");
    var output = Path.Combine(supportRoot, "support.zip");
    SupportPackage.Create(output, "test", log, [preset]);
    using (var archive = System.IO.Compression.ZipFile.OpenRead(output))
    {
        Check(archive.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).SequenceEqual(new[] { "README.txt", "diagnostics/session.log", "presets/preset-01.json" }), "support ZIP contains only its summary, session log and explicitly selected presets with fixed safe entry names");
        using var reader = new StreamReader(archive.GetEntry("diagnostics/session.log")!.Open());
        Check(reader.ReadToEnd() == "test diagnostic fixture", "support ZIP preserves the selected session log");
    }
    var original = File.ReadAllBytes(output); var refused = false;
    try { SupportPackage.Create(output, "test", log, []); } catch (IOException) { refused = true; }
    Check(refused && File.ReadAllBytes(output).SequenceEqual(original), "support packaging never overwrites an existing output");
    var invalid = Path.Combine(supportRoot, "invalid.zip");
    Reject(() => SupportPackage.Create(invalid, "test", log, [log]), "support presets must be explicitly selected JSON files");
    Check(!File.Exists(invalid) && !Directory.EnumerateFiles(supportRoot, "*.partial").Any(), "failed support packaging cleans temporary files and publishes no ZIP");
    var oversize = Path.Combine(supportRoot, "large.json"); File.WriteAllBytes(oversize, new byte[1024 * 1024 + 1]);
    var bounded = false;
    try { SupportPackage.Create(invalid, "test", log, [oversize]); } catch (IOException) { bounded = true; }
    Check(bounded && !File.Exists(invalid), "support preset size limits are enforced before publication");
}
finally { Directory.Delete(supportRoot, true); }

var libraryRoot = Path.Combine(Path.GetTempPath(), "ImageForge-library-fixture");
var proEdition = new ImageEdition(6, "Windows 11 Pro", "Pro fixture");
var homeEdition = new ImageEdition(1, "Windows 11 Home", "Home fixture");
LibrarySource<ImageEdition>[] librarySources =
[
    new(Path.Combine(libraryRoot, "Documents", "02", "sources", "install.wim"), [proEdition, homeEdition]),
    new(Path.Combine(libraryRoot, "Documents", "Original", "install.wim"), [homeEdition]),
    new(Path.Combine(libraryRoot, "Downloads", "install.esd"), [proEdition])
];
var tree = ImageLibrary.Build(librarySources, edition => edition.Index, edition => edition.Name);
var nodes = ImageLibrary.Nodes(tree).ToArray();
Check(tree.Select(node => node.Name).SequenceEqual(new[] { "Read-only", "Other" }), "library categories remain separate and ordered");
Check(tree[1].Children.Count == 1 && tree[1].Children[0].Path == Path.Combine(libraryRoot, "Documents"), "library begins at the actual common source folder without a long redundant drive hierarchy");
Check(nodes.Count(node => node.Kind == LibraryNodeKind.Source && node.Name == "install.wim") == 2, "identically named images in different directories remain distinct source nodes");
Check(nodes.Single(node => node.Path == librarySources[0].Path && node.Kind == LibraryNodeKind.Source).Children.Select(node => node.Edition!.Index).SequenceEqual(new[] { 1, 6 }), "source files expand into genuine numerically ordered editions");
Check(nodes.Single(node => node.Path == librarySources[0].Path && node.Edition?.Index == 6).Edition == proEdition, "tree edition nodes retain the original metadata object used for progress and mount status");
Check(nodes.Select(node => node.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() == nodes.Length, "folder, source and edition identities do not collide");
var filteredTree = ImageLibrary.Build(librarySources, edition => edition.Index, edition => edition.Name, "Windows 11 Pro", "Other");
Check(filteredTree.Count == 1 && ImageLibrary.Nodes(filteredTree).Where(node => node.Kind == LibraryNodeKind.Edition).Select(node => node.Edition!.Index).SequenceEqual(new[] { 6 }), "library filtering retains ancestor folders and only matching editions in the requested category");
var emptyTree = ImageLibrary.Build(Array.Empty<LibrarySource<ImageEdition>>(), edition => edition.Index, edition => edition.Name);
Check(emptyTree.Count == 2 && emptyTree.All(node => node.Children.Count == 1 && node.Children[0].IsMessage), "empty categories have real nonoverlapping message nodes rather than overlay panels");
var noMatches = ImageLibrary.Build(librarySources, edition => edition.Index, edition => edition.Name, "not-an-image");
Check(noMatches.All(node => node.Children.Single().Name == "No matching images."), "unmatched searches do not present fabricated or stale editions");
Check(ImageLibrary.Category("install.ESD") == "Read-only" && ImageLibrary.Category("install.swm") == "Read-only" && ImageLibrary.Category("disk.vhdx") == "Other", "source categories recognize packed/split formats without case sensitivity");
var siblingSources = new[] { new LibrarySource<ImageEdition>(Path.Combine(libraryRoot, "Images", "a.wim"), [homeEdition]), new LibrarySource<ImageEdition>(Path.Combine(libraryRoot, "Images-other", "b.wim"), [homeEdition]) };
Check(ImageLibrary.Build(siblingSources, edition => edition.Index, edition => edition.Name)[1].Children.Single().Path == libraryRoot, "common-folder detection respects directory boundaries instead of matching path prefixes");

Console.WriteLine($"{count} checks passed. No Windows servicing commands were executed.");

sealed class MediaHandler(Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> response) : System.Net.Http.HttpMessageHandler
{
    protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(response(request)); }
}
