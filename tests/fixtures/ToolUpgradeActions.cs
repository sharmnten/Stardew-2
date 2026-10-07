using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace StardewBrowser.Testing;

internal static class ToolUpgradeActions
{
    internal const string FixtureKey = "StardewBrowser.ToolUpgradeFixture";

    internal static void Prepare()
    {
        Game1.player.modData[FixtureKey] = "1";
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
            OpenShop();
            object purchase = Purchase();
            Game1.exitActiveMenu();
            var countdown = new List<int> { Game1.player.daysLeftForToolUpgrade.Value };
            for (int day = 0; day < 2; day++)
            {
                Game1.player.dayupdate(600);
                countdown.Add(Game1.player.daysLeftForToolUpgrade.Value);
            }
            bool handled = Collect();
            var returned = Game1.player.Items.OfType<Axe>().SingleOrDefault();
            var collection = new { handled, pending = Game1.player.toolBeingUpgraded.Value != null,
                id = returned?.QualifiedItemId, level = returned?.UpgradeLevel };
            return new { purchase, countdown, collection };
        }
        finally { Game1.random = savedRandom; }
    }

    internal static void OpenShop()
    {
        if (!Utility.TryOpenShopMenu("ClintUpgrade", "Clint", playOpenSound: false))
            throw new InvalidOperationException("Original tool upgrade shop unavailable.");
        var menu = (ShopMenu)Game1.activeClickableMenu;
        int index = menu.forSale.FindIndex(item => item is Axe axe && axe.UpgradeLevel == 1);
        if (index < 0) throw new InvalidOperationException("Original shop did not offer the copper axe.");
        menu.currentItemIndex = index;
    }

    internal static object Purchase()
    {
        var menu = (ShopMenu)Game1.activeClickableMenu;
        int money = Game1.player.Money, bars = Bars();
        menu.update(new GameTime(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300)));
        var row = menu.forSaleButtons[0].bounds.Center;
        menu.receiveLeftClick(row.X, row.Y);
        var pending = Game1.player.toolBeingUpgraded.Value
            ?? throw new InvalidOperationException("Original purchase did not start the upgrade.");
        return new { id = pending.QualifiedItemId, moneySpent = money - Game1.player.Money,
            barsSpent = bars - Bars(), level = pending.UpgradeLevel,
            originalRemoved = !Game1.player.Items.Any(item => item?.QualifiedItemId == "(T)Axe") };
    }

    internal static bool Collect()
    {
        var counter = Counter();
        return Game1.getLocationFromName("Blacksmith").blacksmith(new xTile.Dimensions.Location(counter.X, counter.Y));
    }

    internal static Point Counter()
    {
        var smith = Game1.getLocationFromName("Blacksmith");
        var layer = smith.map.GetLayer("Buildings");
        for (int y = 0; y < layer.LayerHeight; y++)
            for (int x = 0; x < layer.LayerWidth; x++)
                if (smith.doesTileHaveProperty(x, y, "Action", "Buildings") == "Blacksmith") return new Point(x, y);
        throw new InvalidOperationException("Original blacksmith map has no counter action.");
    }

    internal static object Read() => new { day = Game1.dayOfMonth, money = Game1.player.Money, bars = Bars(),
        pendingId = Game1.player.toolBeingUpgraded.Value?.QualifiedItemId,
        pendingLevel = Game1.player.toolBeingUpgraded.Value?.UpgradeLevel,
        days = Game1.player.daysLeftForToolUpgrade.Value,
        axes = Game1.player.Items.OfType<Axe>().Select(axe => new { id = axe.QualifiedItemId, level = axe.UpgradeLevel }).ToArray() };

    internal static object Diagnostics() => new { pendingType = Game1.player.toolBeingUpgraded.Value?.GetType().FullName,
        items = Game1.player.Items.Select(item => item == null ? null : new { type = item.GetType().FullName, id = item.QualifiedItemId, stack = item.Stack }).ToArray(),
        animation = Game1.player.FarmerSprite.currentAnimationIndex, freeze = Game1.player.freezePause,
        dialogueUp = Game1.dialogueUp };

    private static int Bars() => Game1.player.Items.Where(item => item?.QualifiedItemId == "(O)334")
        .Sum(item => item!.Stack);
}
