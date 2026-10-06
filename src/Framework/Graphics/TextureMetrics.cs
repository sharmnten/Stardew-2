using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StardewBrowser.Framework.Graphics;

public sealed record TextureLayout(int Width, int Height, int ActualWidth, int ActualHeight)
{
    public Rectangle Bounds => new(0, 0, Width, Height);
    public static TextureLayout FromPacked(uint width, uint height) => new(
        (int)(width >> 16 == 0 ? width : width >> 16),
        (int)(height >> 16 == 0 ? height : height >> 16),
        (int)(width & 0xffff), (int)(height & 0xffff));
}

/// <summary>Retains the original logical dimensions while KNI owns the physical GPU texture.</summary>
public static class TextureMetrics
{
    private sealed class Metadata(Texture2D texture)
    {
        public TextureLayout Layout = new(texture.Width, texture.Height, texture.Width, texture.Height);
        public readonly int SortingKey = (int)sortingKey.GetValue(texture)!;
    }
    private static readonly PropertyInfo sortingKey = typeof(Texture).GetProperty("SortingKey", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMemberException("Pinned KNI Texture.SortingKey is absent.");
    private static readonly ConditionalWeakTable<Texture2D, Metadata> metadata = new();
    private static Metadata Get(Texture2D texture) => metadata.GetValue(texture, static value => new(value));
    public static int Width(Texture2D texture) => Get(texture).Layout.Width;
    public static int Height(Texture2D texture) => Get(texture).Layout.Height;
    public static Rectangle Bounds(Texture2D texture) => Get(texture).Layout.Bounds;
    public static int ActualWidth(Texture2D texture) => texture.Width;
    public static int ActualHeight(Texture2D texture) => texture.Height;
    public static int BrowserWidth(this Texture2D texture) => Width(texture);
    public static int BrowserHeight(this Texture2D texture) => Height(texture);
    public static Rectangle BrowserBounds(this Texture2D texture) => Bounds(texture);
    public static int SortingKey(Texture2D texture) => Get(texture).SortingKey;
    public static float TexelWidth(Texture2D texture) => 1f / texture.Width;
    public static float TexelHeight(Texture2D texture) => 1f / texture.Height;
    public static void SetImageSize(Texture2D texture, int width, int height) =>
        Get(texture).Layout = new(width, height, texture.Width, texture.Height);
}
