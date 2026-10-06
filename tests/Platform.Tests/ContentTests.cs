using System.Net;
using System.Security.Cryptography;
using StardewBrowser.Platform.Content;
using Xunit;

public class ContentTests
{
    private static readonly byte[] Original = "original map bytes"u8.ToArray();
    private static BrowserContentStore Store(byte[] downloaded, string? hash = null, Task? gate = null)
    {
        var entry = new ContentEntry("Maps/Farm", "Content/Maps/Farm.xnb",
            hash ?? Convert.ToHexString(SHA256.HashData(Original)).ToLowerInvariant(), Original.Length, "content");
        return new BrowserContentStore(new HttpClient(new Files(downloaded, gate)) { BaseAddress = new Uri("https://static.invalid/") },
            new ContentManifest(1, "1.6.15.24356", "fb0155d3efb94fdcda1f26ee1b048898fd568257732b4e47ee15e03f266cca11", [entry]));
    }

    [Fact]
    public async Task PreservesVerifiedAssetBytesAcrossOriginalPathAliases()
    {
        var store = Store(Original);
        await store.PreloadAsync(["Maps\\Farm"], CancellationToken.None);
        using var source = store.Open("maps/./farm.xnb");
        using var copy = new MemoryStream();
        source.CopyTo(copy);
        Assert.Equal(Original, copy.ToArray());
    }

    [Fact]
    public async Task ChecksumFailureDoesNotExposeCorruptContent()
    {
        var store = Store("altered map bytes!"u8.ToArray());
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => store.PreloadAsync(["Maps/Farm"], CancellationToken.None));
        Assert.Contains("Maps/Farm", error.Message);
        Assert.Throws<InvalidOperationException>(() => store.Open("Maps/Farm"));
    }

    [Fact]
    public async Task MissingAssetNamesProduceAnActionableError()
    {
        var store = Store(Original);
        var error = await Assert.ThrowsAsync<FileNotFoundException>(() => store.PreloadAsync(["Maps/Missing"], CancellationToken.None));
        Assert.Contains("Maps/Missing", error.Message);
    }

    [Fact]
    public async Task RejectsPathsOutsideTheContentRoot()
    {
        var store = Store(Original);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.PreloadAsync(["../../Maps/Farm"], CancellationToken.None));
    }

    [Fact]
    public void OpeningAnUnloadedAssetCannotStartSynchronousNetworkIo()
    {
        var store = Store(Original);
        Assert.Throws<InvalidOperationException>(() => store.Open("Maps/Farm"));
    }

    [Fact]
    public async Task OverlappingPreloadsShareOneVerifiedResidentAsset()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = Store(Original, gate: gate.Task);
        var first = store.PreloadAsync(["Maps/Farm"], CancellationToken.None);
        var second = store.PreloadAsync(["Maps\\Farm"], CancellationToken.None);
        gate.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(Original.Length, store.ResidentBytes);
        using var content = store.Open("Maps/Farm");
        Assert.Equal(Original.Length, content.Length);
    }

    private sealed class Files(byte[] bytes, Task? gate) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (gate != null) await gate;
            else await Task.Yield();
            return request.RequestUri!.AbsolutePath == "/Content/Maps/Farm.xnb"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
