using Microsoft.JSInterop;

namespace StardewBrowser.Platform.Storage;

public sealed record SaveSnapshot(string Slot, Dictionary<string, byte[]> Files);

public interface ISaveBackend
{
    Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync();
    Task<SaveSnapshot?> ReadAsync(string slot);
    Task CommitAsync(SaveSnapshot snapshot);
    Task DeleteAsync(string slot);
}

public sealed class IndexedDbSaveBackend(IJSRuntime js) : ISaveBackend
{
    public async Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync() => await js.InvokeAsync<SaveSnapshot[]>("portStorage.readAllForDotNet");
    public async Task<SaveSnapshot?> ReadAsync(string slot) => await js.InvokeAsync<SaveSnapshot?>("portStorage.readForDotNet", slot);
    public async Task CommitAsync(SaveSnapshot snapshot) => await js.InvokeVoidAsync("portStorage.commit", snapshot);
    public async Task DeleteAsync(string slot) => await js.InvokeVoidAsync("portStorage.deleteSlot", slot);
}

/// <summary>Retains original serializer bytes; the backend owns atomic current/previous snapshots.</summary>
public sealed class BrowserSaveStore(ISaveBackend backend, string root = BrowserSaveStore.Root, Action<IReadOnlyDictionary<string, byte[]>>? validateImport = null)
{
    public const string Root = "/stardew-user";
    public const string SettingsSlot = "@settings";

    public async Task HydrateAsync()
    {
        var snapshots = await backend.ReadAllAsync();
        // Check every path before writing any hydrated files.
        foreach (var snapshot in snapshots) Validate(snapshot.Slot, snapshot.Files, snapshot.Slot == SettingsSlot);
        foreach (var snapshot in snapshots) WriteFiles(snapshot);
    }

    public Task CommitAsync(string slot, IReadOnlyDictionary<string, byte[]> files)
    {
        Validate(slot, files);
        return backend.CommitAsync(Copy(slot, files));
    }

    public Task CommitSettingsAsync(IReadOnlyDictionary<string, byte[]> files)
    {
        Validate(SettingsSlot, files, settings: true);
        return backend.CommitAsync(Copy(SettingsSlot, files));
    }

    public async Task ImportAsync(IReadOnlyDictionary<string, byte[]> files)
    {
        string[] candidates = files.Keys.Where(name => name != "SaveGameInfo" && !name.EndsWith("_old", StringComparison.Ordinal)).ToArray();
        if (candidates.Length != 1) throw new InvalidDataException("Select the farm save and SaveGameInfo from the same save folder.");
        string slot = candidates[0];
        Validate(slot, files);
        if (validateImport == null) throw new InvalidOperationException("Original save validation has not been configured.");
        validateImport(files);
        var snapshot = Copy(slot, files);
        await backend.CommitAsync(snapshot);
        WriteFiles(snapshot);
    }

    public async Task<IReadOnlyDictionary<string, byte[]>> ExportAsync(string slot)
    {
        ValidateSlot(slot);
        var snapshot = await backend.ReadAsync(slot) ?? throw new FileNotFoundException("No persisted save exists for " + slot);
        Validate(slot, snapshot.Files);
        return Copy(slot, snapshot.Files).Files;
    }

    public async Task DeleteAsync(string slot)
    {
        ValidateSlot(slot);
        await backend.DeleteAsync(slot);
        string directory = Path.Combine(root, "Saves", slot);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    public async Task<string[]> ListSlotsAsync() => (await backend.ReadAllAsync())
        .Where(snapshot => snapshot.Slot != SettingsSlot).Select(snapshot => snapshot.Slot).Order().ToArray();

    private void WriteFiles(SaveSnapshot snapshot)
    {
        string directory = snapshot.Slot == SettingsSlot ? root : Path.Combine(root, "Saves", snapshot.Slot);
        Directory.CreateDirectory(directory);
        // Replace the entire hydrated snapshot, including removal of obsolete backup files.
        if (snapshot.Slot != SettingsSlot)
            foreach (string path in Directory.GetFiles(directory)) File.Delete(path);
        foreach (var file in snapshot.Files) File.WriteAllBytes(Path.Combine(directory, file.Key), file.Value);
    }

    private static SaveSnapshot Copy(string slot, IReadOnlyDictionary<string, byte[]> files) =>
        new(slot, files.ToDictionary(file => file.Key, file => file.Value.ToArray(), StringComparer.Ordinal));

    public static void ValidateSlot(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot) || slot is "." or ".." || slot.Contains('/') || slot.Contains('\\')
            || slot.StartsWith('@') || slot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidDataException("Invalid save folder: " + slot);
    }

    public static void Validate(string slot, IReadOnlyDictionary<string, byte[]> files, bool settings = false)
    {
        if (!settings) ValidateSlot(slot);
        if (settings && slot != SettingsSlot) throw new InvalidDataException("Invalid settings folder.");
        var allowed = settings ? new[] { "startup_preferences" } : new[] { slot, "SaveGameInfo", slot + "_old", "SaveGameInfo_old" };
        if (files.Any(file => !allowed.Contains(file.Key, StringComparer.Ordinal) || file.Value == null || file.Value.Length == 0)
            || !files.ContainsKey(allowed[0]) || (!settings && !files.ContainsKey("SaveGameInfo")))
            throw new InvalidDataException("A save must contain the original farm file and SaveGameInfo, with matching optional backups.");
    }
}
