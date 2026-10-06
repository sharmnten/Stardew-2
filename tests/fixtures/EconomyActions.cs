using System.Reflection;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace StardewBrowser.Testing;

internal static class EconomyActions
{
    private static readonly Vector2 ChestTile = new(24, 20);

    internal static void Prepare()
    {
        for (int i = 0; i < Game1.player.Items.Count; i++) Game1.player.Items[i] = null;
        Game1.player.Items[0] = ItemRegistry.Create("(O)24", 20);
        Game1.player.Money = 500; Game1.player.festivalScore = 200;
        Game1.player.clubCoins = 300; Game1.player.QiGems = 100;
        var chest = new Chest(true, ChestTile);
        chest.GetItemsForPlayer().Add(ItemRegistry.Create("(O)24", 990));
        chest.GetItemsForPlayer().Add(ItemRegistry.Create("(O)24", 2, quality: 2));
        Game1.getFarm().objects[ChestTile] = chest;
    }

    internal static object Run()
    {
        var chest = (Chest)Game1.getFarm().objects[ChestTile];
        Game1.player.Items[0] = chest.addItem(Game1.player.Items[0]);
        var stacks = chest.GetItemsForPlayer().OrderBy(item => item.Quality).ThenByDescending(item => item.Stack)
            .Select(item => new { id = item.QualifiedItemId, quality = item.Quality, stack = item.Stack }).ToArray();
        int[] prices = new[] { 0, 1, 2, 4 }.Select(quality => ItemRegistry.Create("(O)24", quality: quality).sellToStorePrice()).ToArray();
        if (!Utility.TryOpenShopMenu("SeedShop", "Pierre", playOpenSound: false)) throw new InvalidOperationException("Original seed shop unavailable.");
        var menu = (ShopMenu)Game1.activeClickableMenu;
        int seedIndex = menu.forSale.FindIndex(item => item.QualifiedItemId == "(O)472");
        if (seedIndex < 0) throw new InvalidOperationException("Original shop did not stock parsnip seeds.");
        var seed = menu.forSale[seedIndex];
        int seedPrice = menu.itemPriceAndStock[seed].Price, before = Game1.player.Money;
        menu.update(new GameTime(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300)));
        menu.currentItemIndex = seedIndex;
        var row = menu.forSaleButtons[0].bounds.Center;
        menu.receiveLeftClick(row.X, row.Y);
        var shop = new { seedPrice, moneySpent = before - Game1.player.Money,
            purchasedId = menu.heldItem?.QualifiedItemId ?? Game1.player.Items.FirstOrDefault(item => item?.QualifiedItemId == "(O)472")?.QualifiedItemId };
        menu.exitThisMenu();
        // Festival tokens are runtime state, reset by the original loader.
        Game1.player.festivalScore = 200;
        foreach (int currency in new[] { 1, 2, 4 }) ShopMenu.chargePlayer(Game1.player, currency, 10);
        int[] currencies = new[] { 0, 1, 2, 4 }.Select(currency => ShopMenu.getPlayerCurrencyAmount(Game1.player, currency)).ToArray();
        var shipping = new ShippingMenu(new List<Item> { ItemRegistry.Create("(O)24", 3), ItemRegistry.Create("(O)24", 1, quality: 2) });
        int shippingTotal = ((List<int>)typeof(ShippingMenu).GetField("categoryTotals", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shipping)!)[5];
        return new { stacks, chestCapacity = chest.GetActualCapacity(), prices, shop, currencies, shippingTotal };
    }
}
