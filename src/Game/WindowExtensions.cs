using System;
using Microsoft.Xna.Framework;

namespace StardewBrowser.Framework.Graphics;

public static class WindowExtensions
{
    public static int GetDisplayIndex(this GameWindow window) => 0;
    public static bool CenterOnDisplay(this GameWindow window, int index) => index == 0;
    public static Rectangle GetDisplayBounds(this GameWindow window, int index) => index == 0
        ? window.ClientBounds : throw new ArgumentOutOfRangeException(nameof(index));
}
