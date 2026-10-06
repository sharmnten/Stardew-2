using Microsoft.Xna.Framework.Graphics;
using StardewBrowser.Platform.Compatibility;

namespace StardewBrowser.Framework.Graphics;

internal static class RendererBoundary
{
    internal static readonly byte[] SpriteEffectBytecode = LoadShader();
    private static byte[] LoadShader()
    {
        using var resource = typeof(RendererBoundary).Assembly.GetManifestResourceStream("OriginalSpriteEffect")
            ?? throw new InvalidDataException("Missing original sprite shader.");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        return LegacyEffect.Convert(buffer.ToArray());
    }
    internal static SpriteFont.Glyph Glyph(SpriteFont font, char character)
    {
        if (font.Glyphs.TryGetValue(character, out var glyph)) return glyph;
        if (font.DefaultCharacter is char fallback && font.Glyphs.TryGetValue(fallback, out glyph)) return glyph;
        throw new ArgumentException("Text contains characters that cannot be resolved by this SpriteFont.", "text");
    }
    internal static long SpritesSubmitted;
    internal static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
}
