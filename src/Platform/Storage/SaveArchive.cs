using System.IO.Compression;

namespace StardewBrowser.Platform.Storage;

public static class SaveArchive
{
    public const long MaxFileBytes = 128L * 1024 * 1024;
    public static byte[] Create(string slot, IReadOnlyDictionary<string, byte[]> files)
    {
        BrowserSaveStore.Validate(slot, files);
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var file in files)
            {
                using var destination = archive.CreateEntry(slot + "/" + file.Key, CompressionLevel.Fastest).Open();
                destination.Write(file.Value);
            }
        return stream.ToArray();
    }

    public static Dictionary<string, byte[]> Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        string? directory = null;
        if (archive.Entries.Count > 5) throw new InvalidDataException("Select a ZIP containing one farm save folder.");
        foreach (var entry in archive.Entries)
        {
            var path = entry.FullName.Split('/');
            if (path.Length == 2 && path[1].Length == 0) { BrowserSaveStore.ValidateSlot(path[0]); continue; }
            if (path.Length is < 1 or > 2 || path.Any(part => part is "" or "." or ".." || part.Contains('\\'))
                || entry.Length is <= 0 or > MaxFileBytes)
                throw new InvalidDataException("Invalid save archive entry: " + entry.FullName);
            if (path.Length == 2)
            {
                BrowserSaveStore.ValidateSlot(path[0]);
                if (directory != null && directory != path[0]) throw new InvalidDataException("A save archive must contain one folder.");
                directory = path[0];
            }
            using var source = entry.Open();
            using var buffer = new MemoryStream();
            source.CopyTo(buffer);
            if (!files.TryAdd(path[^1], buffer.ToArray())) throw new InvalidDataException("Duplicate save archive entry.");
        }
        string[] candidates = files.Keys.Where(name => name != "SaveGameInfo" && !name.EndsWith("_old", StringComparison.Ordinal)).ToArray();
        if (candidates.Length != 1 || (directory != null && directory != candidates[0]))
            throw new InvalidDataException("The save archive folder and farm filename must match.");
        BrowserSaveStore.Validate(candidates[0], files);
        return files;
    }
}
