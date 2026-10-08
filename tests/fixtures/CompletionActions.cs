using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;
using StardewValley.SpecialOrders;

namespace StardewBrowser.Testing;

/// <summary>Advanced prerequisites, original actions, and read-only observations.</summary>
internal static class CompletionActions
{
    internal const string FixtureKey = "StardewBrowser.CompletionFixture";

    internal static void Prepare()
    {
        Game1.player.modData[FixtureKey] = "1";
        Game1.year = 3; Game1.season = Season.Spring; Game1.dayOfMonth = 1;
        Game1.stats.DaysPlayed = 225;
        // This year-three save has already seen Grandpa's evaluation. Keeping
        // it unseen starts original event 558291 during Load, before this case.
        Game1.player.eventsSeen.Add("558291");
        Game1.player.eventsSeen.Add("558292");
        uint earned = Game1.player.totalMoneyEarned;
        Game1.player.Money = 600000;
        Game1.player.totalMoneyEarned = earned;
        Game1.player.mailReceived.Add("gotPet");
        Game1.player.mailReceived.Add("FizzIntro");
        Game1.player.mailReceived.Add("FizzFirstDialogue");
        Game1.player.mailReceived.Add("willyBoatFixed");
        foreach (string room in new[] { "BoilerRoom", "CraftsRoom", "Pantry", "FishTank", "Vault", "Bulletin" })
            Game1.player.mailReceived.Add("cc" + room);
        Game1.netWorldState.Value.PerfectionWaivers = 99;
        var home = Utility.getHomeOfFarmer(Game1.player);
        home.moveObjectsForHouseUpgrade(2);
        Game1.player.HouseUpgradeLevel = 2;
        home.setMapForUpgradeLevel(2);
        var order = SpecialOrder.GetSpecialOrder("QiChallenge2", 1729)
            ?? throw new InvalidOperationException("Original Qi crop order is missing.");
        Game1.player.team.specialOrders.Add(order);
        order.Update();
        // Starting shipment, not credited progress: the original overnight
        // shipping parser must increment the objective and award its reward.
        Game1.getFarm().getShippingBin(Game1.player).Add(ItemRegistry.Create("(O)" + DataLoader.Objects(Game1.content).Single(pair => pair.Value.Name == "Qi Fruit").Key, 500));
        Game1.netWorldState.Value.UpdateFromGame1();
    }

    private static HouseRenovation BedroomRenovation()
    {
        var data = DataLoader.HomeRenovations(Game1.content);
        return HouseRenovation.GetAvailableRenovations().OfType<HouseRenovation>().Single(renovation =>
            data[renovation.Name].RenovateActions.Any(action => action.Type == "Mail"
                && action.Key == "renovation_bedroom_open" && action.Value == "1"));
    }

    internal static object OpenRenovation()
    {
        var renovation = BedroomRenovation();
        Game1.activeClickableMenu = new RenovateMenu(renovation);
        var center = renovation.renovationBounds[0][0].Center;
        return new { x = center.X * 64 + 32, y = center.Y * 64 + 32, price = renovation.Price };
    }

    internal static void Renovate()
    {
        var renovation = BedroomRenovation();
        var center = renovation.renovationBounds[0][0].Center;
        var menu = new RenovateMenu(renovation);
        Game1.activeClickableMenu = menu;
        int x = (int)Utility.ModifyCoordinateForUIScale(center.X * 64 + 32 - Game1.viewport.X);
        int y = (int)Utility.ModifyCoordinateForUIScale(center.Y * 64 + 32 - Game1.viewport.Y);
        Game1.fadeClear();
        menu.performHoverAction(x, y);
        menu.receiveLeftClick(x, y);
    }

    internal static object FizzPosition()
    {
        var fizz = Game1.currentLocation.getCharacterFromName("Fizz")
            ?? throw new InvalidOperationException("Original island entry did not spawn Fizz.");
        var center = fizz.GetBoundingBox().Center;
        return new { x = center.X, y = center.Y };
    }

    internal static object Read()
    {
        var order = Game1.player.team.specialOrders.FirstOrDefault(order => order.questKey.Value == "QiChallenge2");
        return new {
            day = Game1.dayOfMonth, year = Game1.year, money = Game1.player.Money,
            waivers = Game1.netWorldState.Value.PerfectionWaivers,
            perfect = Game1.player.team.farmPerfect.Value,
            eternal = Game1.player.mailReceived.Contains("Farm_Eternal"),
            bedroomOpen = Game1.player.mailReceived.Contains("renovation_bedroom_open"),
            houseLevel = Game1.player.HouseUpgradeLevel, qiGems = Game1.player.QiGems,
            completedOrders = Game1.player.team.completedSpecialOrders.Order().ToArray(),
            order = order == null ? null : new { id = order.questKey.Value, state = order.questState.Value.ToString(),
                count = order.objectives.Single().GetCount(), required = order.objectives.Single().GetMaxCount() },
            summitSeen = Game1.player.mailReceived.Contains("Summit_event"),
            summitAchievement = Game1.player.achievements.Contains(44),
            endingSong = Game1.player.songsHeard.Contains("end_credits")
        };
    }
}
