using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ImageForge.Core;

public sealed record DownloadProgress(long BytesReceived, long? TotalBytes)
{
    public double? Percentage => TotalBytes is > 0 ? Math.Min(100, 100d * BytesReceived / TotalBytes.Value) : null;
}

public sealed class MicrosoftMediaDownload : IDisposable
{
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "software.download.prss.microsoft.com", "software-download.microsoft.com", "download.microsoft.com",
        "dl.delivery.mp.microsoft.com", "tlu.dl.delivery.mp.microsoft.com", "catalog.s.download.windowsupdate.com"
    };
    private readonly HttpClient client;
    public MicrosoftMediaDownload(HttpMessageHandler? handler = null)
    {
        client = new(handler ?? new SocketsHttpHandler { AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(30) });
        client.Timeout = TimeSpan.FromMinutes(2);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ImageForge/0.3");
    }
    public static Uri ValidateUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0 || !AllowedHosts.Contains(uri.IdnHost))
            throw new ArgumentException("Use a direct HTTPS download link from Microsoft's official Windows download page. Other download hosts are not accepted.");
        return uri;
    }
    public static string ValidateSha256(string value)
    {
        value = value.Trim();
        if (!Regex.IsMatch(value, "^[A-Fa-f0-9]{64}$")) throw new ArgumentException("Paste the 64-character SHA-256 hash for this exact Microsoft release, language and architecture.");
        return value.ToLowerInvariant();
    }
    public async Task DownloadAsync(string url, string expectedSha256, string destination,
        IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var uri = ValidateUri(url); var hash = ValidateSha256(expectedSha256);
        var ext = Path.GetExtension(destination).ToLowerInvariant();
        if (ext is not (".iso" or ".esd")) throw new ArgumentException("Save Windows media as a new .iso or .esd file.");
        destination = ImageTools.NewOutputFile(destination, ext);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using var response = await GetResponseAsync(uri, cancellationToken);
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is "text/html" or "application/json" || mediaType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true)
                throw new InvalidDataException("The link returned a web page rather than Windows media. Generate a fresh direct download link on Microsoft's page.");
            var total = response.Content.Headers.ContentLength;
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long received = 0;
            var lastProgress = System.Diagnostics.Stopwatch.StartNew();
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
            {
                var buffer = new byte[1024 * 1024];
                using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                while (true)
                {
                    idle.CancelAfter(TimeSpan.FromMinutes(2));
                    var read = await input.ReadAsync(buffer, idle.Token);
                    if (read == 0) break;
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    digest.AppendData(buffer, 0, read); received += read;
                    if (lastProgress.ElapsedMilliseconds >= 150 || received == total)
                    { progress?.Report(new(received, total)); lastProgress.Restart(); }
                }
                await output.FlushAsync(cancellationToken);
            }
            if (received == 0 || total is { } length && length != received)
                throw new InvalidDataException("The download did not contain the expected number of bytes. No media file was saved.");
            if (!CryptographicOperations.FixedTimeEquals(digest.GetHashAndReset(), Convert.FromHexString(hash)))
                throw new InvalidDataException("The downloaded file does not match Microsoft's supplied SHA-256 hash. No media file was saved.");
            ValidateContainer(temporary, ext);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void ValidateContainer(string path, string extension)
    {
        using var file = File.OpenRead(path);
        if (extension == ".esd")
        {
            var header = new byte[8];
            if (file.Length < 8 || file.Read(header) != 8 || !header.AsSpan().SequenceEqual("MSWIM\0\0\0"u8))
                throw new InvalidDataException("The verified download is not an ESD/WIM container. Check the selected output format and Microsoft link.");
            return;
        }
        var marker = new byte[5];
        for (var sector = 16; sector < 32; sector++)
        {
            var offset = sector * 2048L + 1;
            if (file.Length < offset + marker.Length) break;
            file.Position = offset; file.ReadExactly(marker);
            if (marker.AsSpan().SequenceEqual("CD001"u8) || marker.AsSpan().SequenceEqual("BEA01"u8) || marker.AsSpan().SequenceEqual("NSR02"u8) || marker.AsSpan().SequenceEqual("NSR03"u8)) return;
        }
        throw new InvalidDataException("The verified download is not an ISO/UDF container. Choose the correct output format and direct Microsoft media link.");
    }
    private async Task<HttpResponseMessage> GetResponseAsync(Uri uri, CancellationToken cancellationToken)
    {
        for (var redirects = 0; redirects <= 8; redirects++)
        {
            var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location is null) throw new HttpRequestException("Microsoft returned an empty download redirect.");
                uri = ValidateUri(new Uri(uri, location).AbsoluteUri);
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode; response.Dispose();
                throw new HttpRequestException($"Microsoft returned HTTP {status}. Generate a fresh download link and try again.");
            }
            return response;
        }
        throw new HttpRequestException("The download exceeded the redirect limit.");
    }
    public void Dispose() => client.Dispose();
}
