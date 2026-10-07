using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;
using NativeObject = StardewValley.Object;

namespace StardewBrowser.Testing;

internal static class TextSignActions
{
    private static readonly Vector2 Tile = new(4, 7);

    internal static void Prepare()
    {
        var home = Game1.getLocationFromName("FarmHouse");
        foreach (var furniture in home.furniture.Where(item => item.GetBoundingBox()
            .Intersects(new Rectangle(4 * 64, 7 * 64, 64, 64))).ToArray())
            home.furniture.Remove(furniture);
        var sign = ItemRegistry.Create<NativeObject>("(BC)TextSign");
        sign.signText.Value = "Old";
        home.objects[Tile] = sign;
    }

    internal static object Run()
    {
        Game1.getLocationFromName("FarmHouse").objects[Tile].checkForAction(Game1.player);
        var editor = (TitleTextInputMenu)Game1.activeClickableMenu;
        editor.textBox.Text = "Fresh farm sign";
        editor.textBoxEnter(editor.textBox);
        return Read();
    }

    internal static object Read() => new { x = (int)Tile.X, y = (int)Tile.Y,
        text = Game1.getLocationFromName("FarmHouse").objects[Tile].signText.Value };
}
