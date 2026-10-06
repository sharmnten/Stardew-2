using System.Reflection;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace StardewBrowser.Framework.Graphics;

public static class OriginalContentReaders
{
    public static int TextureReads;
    public static void Register()
    {
        // Pinned KNI exposes no reader factory registration API. Bind its cache
        // once before content loading; original reader names still resolve normally.
        var nativeReader = typeof(Texture2D).Assembly.GetType("Microsoft.Xna.Framework.Content.Texture2DReader")
            ?? throw new MissingMemberException("Pinned KNI texture reader is absent.");
        var cache = (Dictionary<Type, ContentTypeReader>)(typeof(ContentTypeReaderManager).GetField("_contentReadersCache",
            BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null)
            ?? throw new MissingMemberException("Pinned KNI reader registration cache is absent."));
        cache[nativeReader] = new Texture2DReader();
    }
}
