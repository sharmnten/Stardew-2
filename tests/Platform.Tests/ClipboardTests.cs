using Microsoft.JSInterop;
using StardewBrowser.Platform.Services;
using Xunit;

public sealed class ClipboardTests
{
    [Fact]
    public void PasteButtonWaitsForFreshTextAndReplacesWithoutAlsoAppending()
    {
        OfflinePlatformServices.Configure(new ClipboardJs());
        BrowserClipboard.Receive("old clipboard");
        while (BrowserClipboard.TryTakePaste(out _)) { }
        string field = "original field";
        BrowserClipboard.RequestReplacement(text => field = text);
        Assert.Equal("original field", field);
        BrowserClipboard.Receive("fresh clipboard", replacement: true);
        Assert.Equal("fresh clipboard", field);
        Assert.False(BrowserClipboard.TryTakePaste(out _));
        BrowserClipboard.RequestReplacement(text => field = text);
        BrowserClipboard.CancelReplacement();
        BrowserClipboard.Receive("cancelled result", replacement: true);
        Assert.Equal("fresh clipboard", field);
        BrowserClipboard.Receive("keyboard paste");
        Assert.True(BrowserClipboard.TryTakePaste(out string? appended));
        Assert.Equal("keyboard paste", appended);
    }

    private sealed class ClipboardJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => new(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken token, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
