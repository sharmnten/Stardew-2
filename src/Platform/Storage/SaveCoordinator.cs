namespace StardewBrowser.Platform.Storage;

public sealed record SaveStatus(string Phase, string? Slot = null, string? Error = null, long Revision = 0);

/// <summary>A failed durable write stays pending so the original SaveGameMenu cannot report success.</summary>
public sealed class SaveCoordinator(BrowserSaveStore store)
{
    private readonly SemaphoreSlim serial = new(1);
    private TaskCompletionSource? retry;
    private long revision;
    public SaveStatus Status { get; private set; } = new("idle");
    public SaveSnapshot? Pending { get; private set; }
    public BrowserSaveStore Store => store;

    public async Task PersistAsync(SaveSnapshot snapshot)
    {
        // Capture immediately; a retry always persists this draft, never repeats gameplay or serialization.
        snapshot = new(snapshot.Slot, snapshot.Files.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
        BrowserSaveStore.Validate(snapshot.Slot, snapshot.Files, snapshot.Slot == BrowserSaveStore.SettingsSlot);
        await WriteAsync(snapshot.Slot, snapshot, () => snapshot.Slot == BrowserSaveStore.SettingsSlot
            ? store.CommitSettingsAsync(snapshot.Files) : store.CommitAsync(snapshot.Slot, snapshot.Files));
    }

    public Task DeleteAsync(string slot)
    {
        BrowserSaveStore.ValidateSlot(slot);
        return WriteAsync(slot, null, () => store.DeleteAsync(slot));
    }

    private async Task WriteAsync(string slot, SaveSnapshot? pending, Func<Task> write)
    {
        await serial.WaitAsync();
        try
        {
            Pending = pending;
            while (true)
            {
                Status = new("saving", slot, Revision: ++revision);
                try
                {
                    await write();
                    Pending = null;
                    Status = new("saved", slot, Revision: ++revision);
                    return;
                }
                catch (Exception error)
                {
                    retry = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    Status = new("failed", slot, error.Message.Split('\n')[0], ++revision);
                    await retry.Task;
                    retry = null;
                }
            }
        }
        finally { serial.Release(); }
    }

    public void Retry() => retry?.TrySetResult();
}

public static class BrowserPersistence
{
    public static SaveCoordinator Current { get; private set; } = null!;
    public static void Configure(BrowserSaveStore store) => Current = new SaveCoordinator(store);

    public static void QueueSettings()
    {
        string path = Path.Combine(BrowserSaveStore.Root, "startup_preferences");
        if (Current == null || !File.Exists(path)) return;
        _ = Current.PersistAsync(new(BrowserSaveStore.SettingsSlot, new() { ["startup_preferences"] = File.ReadAllBytes(path) }));
    }
}
