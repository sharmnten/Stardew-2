using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using NativeObject = StardewValley.Object;

namespace StardewBrowser.Testing;

internal static class DecorationActions
{
    private static readonly Vector2 MachineTile = new(4, 7);
    private static readonly Vector2 HopperTile = new(4, 6);
    private static readonly Vector2 FurnitureTile = new(6, 7);

    internal static void Prepare()
    {
        var home = Game1.getLocationFromName("FarmHouse");
        home.objects[MachineTile] = ItemRegistry.Create<NativeObject>("(BC)24");
        home.objects[HopperTile] = new Chest(true, HopperTile, "275");
        foreach (var tile in new[] { MachineTile, HopperTile, FurnitureTile })
            foreach (var furniture in home.furniture.Where(item => item.GetBoundingBox()
                .Intersects(new Rectangle((int)tile.X * 64, (int)tile.Y * 64, 64, 64))).ToArray())
                home.furniture.Remove(furniture);
    }

    internal static object Run()
    {
        var home = Game1.getLocationFromName("FarmHouse");
        var hopper = (Chest)home.objects[HopperTile];
        var machine = home.objects[MachineTile];
        hopper.addItem(ItemRegistry.Create("(O)176"));
        int eggs = hopper.Items.Where(item => item?.QualifiedItemId == "(O)176").Sum(item => item.Stack);
        hopper.CheckAutoLoad(Game1.player);
        // Pump the original mutex event, as the normal location update does.
        hopper.GetMutex().Update(home);
        int consumed = eggs - hopper.Items.Where(item => item?.QualifiedItemId == "(O)176").Sum(item => item.Stack);
        var automation = new { inputConsumed = consumed, output = machine.heldObject.Value?.QualifiedItemId,
            minutes = machine.MinutesUntilReady, type = hopper.SpecialChestType.ToString() };
        Item tailored = new TailoringMenu().CraftItem(ItemRegistry.Create("(O)428"), ItemRegistry.Create("(O)24"));
        var tailoring = new { clothing = tailored is Clothing, id = tailored?.QualifiedItemId };
        Furniture chair = Furniture.GetFurnitureInstance("0");
        int before = home.furniture.Count;
        bool placed = chair.placementAction(home, (int)FurnitureTile.X * 64, (int)FurnitureTile.Y * 64, Game1.player);
        return new { hopper = automation, tailoring,
            furniture = new { placed, added = home.furniture.Count - before, id = chair.QualifiedItemId } };
    }
}
