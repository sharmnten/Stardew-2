using Microsoft.Xna.Framework;
using StardewValley;
using NativeObject = StardewValley.Object;

namespace StardewBrowser.Testing;

internal static class ProductionActions
{
    private static readonly Vector2 MachineTile = new(25, 20);

    internal static void Prepare()
    {
        for (int i = 0; i < Game1.player.Items.Count; i++) Game1.player.Items[i] = null;
        Game1.player.Items[0] = ItemRegistry.Create("(O)388", 100);
        Game1.player.Items[1] = ItemRegistry.Create("(O)176", 2);
        Game1.player.craftingRecipes["Chest"] = 0;
        Game1.player.cookingRecipes["Fried Egg"] = 0;
        var machine = ItemRegistry.Create<NativeObject>("(BC)24");
        machine.TileLocation = MachineTile;
        Game1.getFarm().objects[MachineTile] = machine;
    }

    internal static object Run()
    {
        var recipe = new CraftingRecipe("Chest", false);
        bool hadIngredients = recipe.doesFarmerHaveIngredientsInInventory();
        int wood = Count("(O)388");
        Item crafted = recipe.createItem();
        recipe.consumeIngredients(null);
        var crafting = new { hadIngredients, output = crafted.QualifiedItemId, woodConsumed = wood - Count("(O)388") };
        recipe = new CraftingRecipe("Fried Egg", true);
        hadIngredients = recipe.doesFarmerHaveIngredientsInInventory();
        int eggs = Count("(O)176");
        Item cooked = recipe.createItem();
        recipe.consumeIngredients(null);
        var cooking = new { hadIngredients, output = cooked.QualifiedItemId, eggConsumed = eggs - Count("(O)176") };
        var machine = Game1.getFarm().objects[MachineTile];
        eggs = Count("(O)176");
        bool accepted = machine.performObjectDropInAction(Game1.player.Items.First(item => item?.QualifiedItemId == "(O)176"), false, Game1.player);
        int inputConsumed = eggs - Count("(O)176"), minutes = machine.MinutesUntilReady;
        string? output = machine.heldObject.Value?.QualifiedItemId;
        machine.minutesElapsed(minutes);
        return new { crafting, cooking, machine = new { accepted, inputConsumed, output, minutes, ready = machine.readyForHarvest.Value } };
    }

    private static int Count(string id) => Game1.player.Items.Where(item => item?.QualifiedItemId == id).Sum(item => item.Stack);
}
