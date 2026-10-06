using StardewBrowser.Platform.Storage;
using Xunit;

public sealed class SaveStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "stardew-save-test-" + Guid.NewGuid());
    private static Dictionary<string, byte[]> Files(byte value) => new() { ["Farm_123"] = [value, 2, 3], ["SaveGameInfo"] = [4, value] };

    [Fact]
    public async Task ReloadHydratesExactOriginalBytesAndSettings()
    {
        var backend = new MemoryBackend();
        var store = new BrowserSaveStore(backend, root);
        await store.CommitAsync("Farm_123", Files(1));
        await store.CommitSettingsAsync(new Dictionary<string, byte[]> { ["startup_preferences"] = [9, 8, 7] });
        await new BrowserSaveStore(backend, root).HydrateAsync();
        Assert.Equal(Files(1)["Farm_123"], File.ReadAllBytes(Path.Combine(root, "Saves/Farm_123/Farm_123")));
        Assert.Equal(new byte[] { 9, 8, 7 }, File.ReadAllBytes(Path.Combine(root, "startup_preferences")));
        Assert.Equal(Files(1)["SaveGameInfo"], (await store.ExportAsync("Farm_123"))["SaveGameInfo"]);
    }

    [Theory]
    [InlineData("AbortError")]
    [InlineData("QuotaExceededError")]
    public async Task FailedCommitKeepsPreviousSnapshotByteIdenticalAndExportable(string failure)
    {
        var backend = new MemoryBackend();
        var store = new BrowserSaveStore(backend, root);
        await store.CommitAsync("Farm_123", Files(1));
        backend.Failure = failure;
        await Assert.ThrowsAsync<IOException>(() => store.CommitAsync("Farm_123", Files(9)));
        Assert.Equal(Files(1)["Farm_123"], (await store.ExportAsync("Farm_123"))["Farm_123"]);
    }

    [Fact]
    public async Task ImportValidatesBeforeReplacingAnExistingSlot()
    {
        var backend = new MemoryBackend();
        var store = new BrowserSaveStore(backend, root, files => {
            if (files["Farm_123"][0] == 9) throw new InvalidDataException("Invalid original save");
        });
        await store.CommitAsync("Farm_123", Files(1));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ImportAsync(Files(9)));
        Assert.Equal(Files(1)["Farm_123"], (await store.ExportAsync("Farm_123"))["Farm_123"]);
        await store.ImportAsync(Files(3));
        Assert.Equal(Files(3)["Farm_123"], File.ReadAllBytes(Path.Combine(root, "Saves/Farm_123/Farm_123")));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("@settings")]
    public async Task InvalidSlotCannotWriteOutsideSaveRoot(string slot)
    {
        var store = new BrowserSaveStore(new MemoryBackend(), root);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.CommitAsync(slot, Files(1)));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task SaveCompletionWaitsForRetryWithoutRepeatingTheSerializer()
    {
        var backend = new MemoryBackend();
        var store = new BrowserSaveStore(backend, root);
        await store.CommitAsync("Farm_123", Files(1));
        var coordinator = new SaveCoordinator(store);
        backend.Failure = "QuotaExceededError";
        var completion = coordinator.PersistAsync(new SaveSnapshot("Farm_123", Files(9)));
        Assert.False(completion.IsCompleted);
        Assert.Equal("failed", coordinator.Status.Phase);
        Assert.Equal(Files(1)["Farm_123"], (await store.ExportAsync("Farm_123"))["Farm_123"]);
        backend.Failure = null;
        coordinator.Retry();
        await completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("saved", coordinator.Status.Phase);
        Assert.Equal(Files(9)["Farm_123"], (await store.ExportAsync("Farm_123"))["Farm_123"]);
    }

    [Fact]
    public async Task FailedDeleteRetainsTheFarmUntilItsTransactionSucceeds()
    {
        var backend = new MemoryBackend();
        var store = new BrowserSaveStore(backend, root);
        await store.CommitAsync("Farm_123", Files(1));
        await store.HydrateAsync();
        var coordinator = new SaveCoordinator(store);
        backend.Failure = "AbortError";
        var completion = coordinator.DeleteAsync("Farm_123");
        Assert.False(completion.IsCompleted);
        Assert.True(File.Exists(Path.Combine(root, "Saves/Farm_123/Farm_123")));
        Assert.Equal(Files(1)["Farm_123"], (await store.ExportAsync("Farm_123"))["Farm_123"]);
        backend.Failure = null;
        coordinator.Retry();
        await completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(Directory.Exists(Path.Combine(root, "Saves/Farm_123")));
        Assert.Empty(await store.ListSlotsAsync());
    }

    [Fact]
    public async Task IncompletePairCannotOverwriteValidSave()
    {
        var backend = new MemoryBackend();
        var store = new BrowserSaveStore(backend, root);
        await store.CommitAsync("Farm_123", Files(1));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.CommitAsync("Farm_123", new Dictionary<string, byte[]> { ["Farm_123"] = [9] }));
        Assert.Equal(Files(1)["Farm_123"], (await store.ExportAsync("Farm_123"))["Farm_123"]);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private sealed class MemoryBackend : ISaveBackend
    {
        private readonly Dictionary<string, SaveSnapshot> snapshots = [];
        public string? Failure;
        public Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync() => Task.FromResult<IReadOnlyList<SaveSnapshot>>(snapshots.Values.ToArray());
        public Task<SaveSnapshot?> ReadAsync(string slot) => Task.FromResult(snapshots.GetValueOrDefault(slot));
        public Task CommitAsync(SaveSnapshot snapshot)
        {
            if (Failure != null) throw new IOException(Failure);
            snapshots[snapshot.Slot] = snapshot;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(string slot) { if (Failure != null) throw new IOException(Failure); snapshots.Remove(slot); return Task.CompletedTask; }
    }
}
