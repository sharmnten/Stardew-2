using Microsoft.JSInterop;

namespace StardewBrowser.Platform.Services;

/// <summary>Browser substitutes for desktop file downloads and clipboard access; achievements remain original save fields.</summary>
public static class OfflinePlatformServices
{
    internal static IJSRuntime Js { get; private set; } = null!;
    public static void Configure(IJSRuntime js) => Js = js;
    public static void Download(string name, byte[] bytes) => _ = Js.InvokeVoidAsync("portStorage.download", name, bytes);
    public static async Task InitializeAsync()
    {
        var files = await Js.InvokeAsync<ScreenshotFile[]>("portServices.initialize");
        foreach (var file in files)
        {
            if (Path.GetFileName(file.Name) != file.Name || file.Name.Contains('\\') || !file.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid stored screenshot filename.");
            string path = Path.Combine(Storage.BrowserSaveStore.Root, "Screenshots", file.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, file.Bytes);
        }
    }
    public static void BrowseScreenshots() => _ = Js.InvokeVoidAsync("portServices.browseScreenshots");
    public static void OpenLink(string url) => _ = Js.InvokeVoidAsync("portServices.openLink", url);
    internal sealed record ScreenshotFile(string Name, byte[] Bytes);
}

public static class BrowserWindow
{
    public static int Mode { get; private set; }
    public static bool IsFullScreen => ((IJSInProcessRuntime)OfflinePlatformServices.Js).Invoke<bool>("portLifecycle.isFullScreen");
    public static void SetMode(int mode)
    {
        Mode = mode;
        _ = OfflinePlatformServices.Js.InvokeVoidAsync("portLifecycle.setFullscreen", mode != 1);
    }
}

public static class BrowserClipboard
{
    private static readonly Queue<string> pasted = new();
    private static string? cached;
    private static Action<string>? replacement;
    public static void Receive(string text, bool replacement = false)
    {
        cached = text;
        if (!replacement) { pasted.Enqueue(text); return; }
        var apply = BrowserClipboard.replacement;
        BrowserClipboard.replacement = null;
        apply?.Invoke(text);
    }
    public static void RequestReplacement(Action<string> apply)
    {
        replacement = apply;
        _ = OfflinePlatformServices.Js.InvokeVoidAsync("portServices.readClipboard", true);
    }
    public static void CancelReplacement() => replacement = null;
    public static bool TryTakePaste(out string? text) => pasted.TryDequeue(out text);
    public static void RequestPaste() => _ = OfflinePlatformServices.Js.InvokeVoidAsync("portServices.readClipboard");
    public static bool GetText(ref string output) { output = cached!; RequestPaste(); return cached != null; }
    public static bool SetText(string text) { text ??= ""; cached = text; _ = OfflinePlatformServices.Js.InvokeVoidAsync("portServices.writeClipboard", text); return true; }
}

/// <summary>Replaces only the original screenshot compositor and encoder; the game still draws every tile.</summary>
public sealed class BrowserScreenshotCanvas : IDisposable
{
    private readonly IJSInProcessRuntime js;
    private readonly int handle;
    private BrowserScreenshotCanvas(IJSInProcessRuntime js, int handle) { this.js = js; this.handle = handle; }
    public static BrowserScreenshotCanvas Create(int width, int height)
    {
        var js = (IJSInProcessRuntime)OfflinePlatformServices.Js;
        return new(js, js.Invoke<int>("portServices.createScreenshot", width, height));
    }
    public void Blit(int x, int y, int width, int height, byte[] pixels) => js.InvokeVoid("portServices.blitScreenshot", handle, x, y, width, height, pixels);
    public void SaveTo(string path)
    {
        byte[] bytes = js.Invoke<byte[]>("portServices.encodeScreenshot", handle);
        File.WriteAllBytes(path, bytes);
        OfflinePlatformServices.Download(Path.GetFileName(path), bytes);
        _ = OfflinePlatformServices.Js.InvokeVoidAsync("portServices.storeScreenshot", Path.GetFileName(path), bytes);
    }
    public void Dispose() => js.InvokeVoid("portServices.disposeScreenshot", handle);
}
