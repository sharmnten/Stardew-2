using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace StardewBrowser.Testing;

internal static class ToolUpgradeActions
{
    internal static void Prepare()
    {
        Game1.year = 2; Game1.season = Season.Spring; Game1.dayOfMonth = 5;
        Game1.stats.DaysPlayed = 117;
        for (int i = 0; i < Game1.player.Items.Count; i++) Game1.player.Items[i] = null;
        Game1.player.Items[0] = ItemRegistry.Create("(T)Axe");
        Game1.player.Items[1] = ItemRegistry.Create("(O)334", 5);
        uint earnings = Game1.player.totalMoneyEarned;
        Game1.player.Money = 2000;
        Game1.player.totalMoneyEarned = earnings;
        Game1.netWorldState.Value.UpdateFromGame1();
    }

    internal static object Run()
    {
        Random savedRandom = Game1.random;
        try
        {
            Game1.random = new Random(1729);
            if (!Utility.TryOpenShopMenu("ClintUpgrade", "Clint", playOpenSound: false))
                throw new InvalidOperationException("Original tool upgrade shop unavailable.");
            var menu = (ShopMenu)Game1.activeClickableMenu;
            int index = menu.forSale.FindIndex(item => item is Axe axe && axe.UpgradeLevel == 1);
            if (index < 0) throw new InvalidOperationException("Original shop did not offer the copper axe.");
            int money = Game1.player.Money, bars = Bars();
            menu.update(new GameTime(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300)));
            menu.currentItemIndex = index;
            var row = menu.forSaleButtons[0].bounds.Center;
            menu.receiveLeftClick(row.X, row.Y);
            var pending = Game1.player.toolBeingUpgraded.Value
                ?? throw new InvalidOperationException("Original purchase did not start the upgrade.");
            var purchase = new { id = pending.QualifiedItemId, moneySpent = money - Game1.player.Money,
                barsSpent = bars - Bars(), level = pending.UpgradeLevel,
                originalRemoved = !Game1.player.Items.Any(item => item?.QualifiedItemId == "(T)Axe") };
            Game1.exitActiveMenu();
            var countdown = new List<int> { Game1.player.daysLeftForToolUpgrade.Value };
            for (int day = 0; day < 2; day++)
            {
                Game1.player.dayupdate(600);
                countdown.Add(Game1.player.daysLeftForToolUpgrade.Value);
            }
            var smith = Game1.getLocationFromName("Blacksmith");
            bool handled = smith.blacksmith(new xTile.Dimensions.Location(3, 13));
            var returned = Game1.player.Items.OfType<Axe>().SingleOrDefault();
            var collection = new { handled, pending = Game1.player.toolBeingUpgraded.Value != null,
                id = returned?.QualifiedItemId, level = returned?.UpgradeLevel };
            return new { purchase, countdown, collection };
        }
        finally { Game1.random = savedRandom; }
    }

    private static int Bars() => Game1.player.Items.Where(item => item?.QualifiedItemId == "(O)334")
        .Sum(item => item!.Stack);
}
