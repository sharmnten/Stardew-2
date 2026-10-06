using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StardewBrowser.Framework.Graphics;

public static class BrowserDisplay
{
    private static Func<Point> dimensions = () => throw new InvalidOperationException("Browser display is not configured.");
    private static readonly ConstructorInfo modeConstructor = typeof(DisplayMode).GetConstructor(
        BindingFlags.NonPublic | BindingFlags.Instance, [typeof(int), typeof(int), typeof(SurfaceFormat)])
        ?? throw new MissingMemberException("Pinned KNI DisplayMode constructor is absent.");
    public static void Configure(Func<Point> getDimensions) => dimensions = getDimensions;
    public static DisplayMode CurrentMode()
    {
        var size = dimensions();
        return (DisplayMode)modeConstructor.Invoke([size.X, size.Y, SurfaceFormat.Color]);
    }
    public static DisplayMode GetBrowserDisplayMode(this GraphicsAdapter adapter) => CurrentMode();
    public static IEnumerable<DisplayMode> GetBrowserDisplayModes(this GraphicsAdapter adapter) => [CurrentMode()];
}
