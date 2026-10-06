using StardewBrowser.Platform.Compatibility;

namespace StardewBrowser.Platform.Content;

/// <summary>Shared verified source for all original localized content managers.</summary>
public static class OriginalContent
{
    public const string Root = "/stardew";
    private static BrowserContentStore? store;
    public static void Configure(BrowserContentStore content) => store = content;
    public static Stream Open(string name)
    {
        var stream = (store ?? throw new InvalidOperationException("Original content is not configured.")).Open(name);
        if (name.Replace('\\', '/') != "Effects/ShadowRemoveMG3.8.0") return stream;
        using (stream)
        using (var buffer = new MemoryStream())
        {
            stream.CopyTo(buffer);
            return new MemoryStream(LegacyEffect.ConvertXnb(buffer.ToArray()), writable: false);
        }
    }
}
