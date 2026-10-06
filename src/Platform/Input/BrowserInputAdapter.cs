using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Platform.Input;

namespace StardewBrowser.Platform.Input;

public sealed record PointerLayout(int Width, int Height, double Left, double Top, double DisplayWidth, double DisplayHeight)
{
    public Point ToLogical(double x, double y) => new((int)Math.Round((x - Left) * Width / DisplayWidth), (int)Math.Round((y - Top) * Height / DisplayHeight));
    public Point ToClient(double x, double y) => new((int)Math.Round(Left + x * DisplayWidth / Width), (int)Math.Round(Top + y * DisplayHeight / Height));
}

/// <summary>Narrow bridges to the pinned KNI browser input strategy; gameplay keeps its original InputState.</summary>
public static class BrowserInputAdapter
{
    private static PointerLayout layout = new(1, 1, 0, 0, 1, 1);
    private static readonly FieldInfo KeysField = typeof(ConcreteKeyboard).GetField("_keys", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo Position = typeof(ConcreteMouse).GetField("_pos", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo[] Buttons = new[] { "_leftButton", "_rightButton", "_middleButton", "_xButton1", "_xButton2" }
        .Select(name => typeof(ConcreteMouse).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!).ToArray();
    private static readonly MethodInfo Activate = typeof(GameWindow).GetMethod("OnActivated", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo Deactivate = typeof(GameWindow).GetMethod("OnDeactivated", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo SizeChanged = typeof(GameWindow).GetMethod("OnClientSizeChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
    public static void Configure(PointerLayout value) => layout = value;

    public static MouseState GetMouseState()
    {
        var raw = Mouse.GetState();
        var point = layout.ToLogical(raw.X, raw.Y);
        return new MouseState(point.X, point.Y, raw.ScrollWheelValue, raw.LeftButton, raw.MiddleButton, raw.RightButton, raw.XButton1, raw.XButton2);
    }

    public static void SetMousePosition(int x, int y)
    {
        // Browsers cannot warp the hardware cursor; the original game draws its own cursor.
        Position.SetValue(((IPlatformMouse)Mouse.Current).GetStrategy<ConcreteMouse>(), layout.ToClient(x, y));
    }

    public static void ReleaseAll()
    {
        var keyboard = ((IPlatformKeyboard)Keyboard.Current).GetStrategy<ConcreteKeyboard>();
        (KeysField.GetValue(keyboard) as List<Keys>)?.Clear();
        var mouse = ((IPlatformMouse)Mouse.Current).GetStrategy<ConcreteMouse>();
        foreach (var field in Buttons) field.SetValue(mouse, ButtonState.Released);
    }

    public static void SetFocus(GameWindow window, bool focused)
    {
        if (!focused) ReleaseAll();
        (focused ? Activate : Deactivate).Invoke(window, null);
    }

    public static void NotifySizeChanged(GameWindow window) => SizeChanged.Invoke(window, null);
}
