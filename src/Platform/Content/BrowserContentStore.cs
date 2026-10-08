using System.Text.Json.Serialization;
using System.Security.Cryptography;

namespace StardewBrowser.Platform.Content;

public sealed record ContentEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("group")] string Group);

public sealed record ContentManifest(
    [property: JsonPropertyName("schema")] int Schema,
    [property: JsonPropertyName("game_version")] string GameVersion,
    [property: JsonPropertyName("archive_sha256")] string ArchiveSha256,
    [property: JsonPropertyName("assets")] ContentEntry[] Assets);

public sealed class BrowserContentStore
{
    private readonly HttpClient http;
    private readonly Dictionary<string, ContentEntry> index = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> resident = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim preloadLock = new(1, 1);
    public long ResidentBytes { get; private set; }

    public BrowserContentStore(HttpClient http, ContentManifest manifest)
    {
        if (manifest.Schema != 1 || manifest.GameVersion != "1.6.15.24356" ||
            manifest.ArchiveSha256 != "fb0155d3efb94fdcda1f26ee1b048898fd568257732b4e47ee15e03f266cca11")
            throw new InvalidDataException("Content manifest does not match the original game version.");
        this.http = http;
        foreach (var entry in manifest.Assets)
        {
            string name = Normalize(entry.Name);
            string pathName = Normalize(entry.Path.StartsWith("Content/") ? entry.Path[8..] : "");
            if (name != pathName || entry.Size < 0 || entry.Sha256.Length != 64 || !index.TryAdd(name, entry))
                throw new InvalidDataException("Invalid or duplicate content manifest entry: " + entry.Name);
        }
    }

    public async Task PreloadAsync(IEnumerable<string> names, CancellationToken cancellationToken, Action<long, long>? progress = null)
    {
        await preloadLock.WaitAsync(cancellationToken);
        try { await PreloadCoreAsync(names, cancellationToken, progress); }
        finally { preloadLock.Release(); }
    }

    private async Task PreloadCoreAsync(IEnumerable<string> names, CancellationToken cancellationToken, Action<long, long>? progress)
    {
        var pending = new List<(string Key, ContentEntry Entry)>();
        var requestedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string requested in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string key = Normalize(requested);
            if (!index.TryGetValue(key, out var entry)) throw new FileNotFoundException("Missing original content: " + key);
            if (resident.ContainsKey(key)) continue;
            if (requestedKeys.Add(key)) pending.Add((key, entry));
        }
        long completed = 0, total = pending.Sum(item => item.Entry.Size);
        progress?.Invoke(completed, total);
        foreach (var batch in pending.Chunk(8))
        {
            var downloads = await Task.WhenAll(batch.Select(async item => (item.Key, Data: await DownloadAsync(item.Entry, cancellationToken))));
            foreach (var (key, data) in downloads)
            {
                resident.Add(key, data);
                ResidentBytes += data.Length;
                completed += data.Length;
            }
            progress?.Invoke(completed, total);
        }
    }

    private async Task<byte[]> DownloadAsync(ContentEntry entry, CancellationToken cancellationToken)
    {
        string path = string.Join('/', entry.Path.Split('/').Select(Uri.EscapeDataString));
        byte[] data;
        try { data = await StaticAssetDownload.GetBytesAsync(http, path, cancellationToken); }
        catch (HttpRequestException error) { throw new IOException("Could not download original content: " + entry.Name, error); }
        if (data.LongLength != entry.Size || !Convert.ToHexString(SHA256.HashData(data)).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Original content checksum mismatch: " + entry.Name);
        return data;
    }

    public Stream Open(string name)
    {
        string key = Normalize(name);
        if (!index.ContainsKey(key)) throw new FileNotFoundException("Missing original content: " + key);
        if (!resident.TryGetValue(key, out var data)) throw new InvalidOperationException("Original content is not preloaded: " + key);
        return new MemoryStream(data, writable: false);
    }

    private static string Normalize(string name)
    {
        name = name.Replace('\\', '/');
        if (name.StartsWith('/') || name.Contains(':')) throw new InvalidDataException("Content path must remain inside the original content root: " + name);
        var parts = new List<string>();
        foreach (string part in name.Split('/'))
        {
            if (part is "" or ".") continue;
            if (part == "..")
            {
                if (parts.Count == 0) throw new InvalidDataException("Content path escapes the original content root: " + name);
                parts.RemoveAt(parts.Count - 1);
            }
            else parts.Add(part);
        }
        string result = string.Join('/', parts);
        if (result.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase)) result = result[..^4];
        if (result.Length == 0) throw new InvalidDataException("Content name is empty.");
        return result;
    }
}
