using StardewValley;
using StardewValley.Characters;
using StardewValley.Pathfinding;
using Microsoft.Xna.Framework;

namespace StardewBrowser.Testing;

internal static class FamilyActions
{
    internal const string FixtureKey = "StardewBrowser.FamilyFixture";

    internal static void Prepare()
    {
        Game1.player.modData[FixtureKey] = "1";
        Game1.player.Items[0] = ItemRegistry.Create("(O)88", quality: 2);
        Game1.year = 2; Game1.season = Season.Spring; Game1.dayOfMonth = 5;
        Game1.stats.DaysPlayed = 117;
        Game1.player.friendshipData["Linus"] = new Friendship(750);
        Game1.player.spouse = "Abigail";
        Game1.player.friendshipData["Abigail"] = new Friendship(2750) {
            Status = FriendshipStatus.Married, WeddingDate = new WorldDate(1, "spring", 1)
        };
        var home = Utility.getHomeOfFarmer(Game1.player);
        home.moveObjectsForHouseUpgrade(2);
        Game1.player.HouseUpgradeLevel = 2;
        home.setMapForUpgradeLevel(2);
        var child = new Child("PortChild", true, false, Game1.player) { currentLocation = home, Age = 0 };
        child.daysOld.Value = 12;
        home.characters.Add(child);
        Game1.netWorldState.Value.UpdateFromGame1();
    }

    internal static void PrepareBirth()
    {
        Prepare();
        // The original global plane event takes priority over personal birth.
        // This advanced family has already witnessed that earlier unlock.
        Game1.player.mailReceived.Add("sawQiPlane");
        var home = Utility.getHomeOfFarmer(Game1.player);
        home.characters.RemoveWhere(character => character is Child);
        Game1.player.GetSpouseFriendship().NextBirthingDate = new WorldDate(2, Season.Spring, 6);
    }

    internal static void GiveHeldGift()
    {
        Game1.player.CurrentToolIndex = 0;
        var linus = Game1.getCharacterFromName("Linus");
        if (!linus.checkAction(Game1.player, Game1.currentLocation))
            throw new InvalidOperationException("Original Linus held-gift interaction failed.");
    }

    internal static Point[] BedRoute()
    {
        var home = Utility.getHomeOfFarmer(Game1.player);
        if (Game1.currentLocation != home || home.GetPlayerBed() == null)
            throw new InvalidOperationException("The original home and player bed are required for its walking route.");
        return PathFindController.findPath(Game1.player.TilePoint, home.GetPlayerBedSpot(),
            PathFindController.isAtEndPoint, home, Game1.player, 10000)?.ToArray()
            ?? throw new InvalidOperationException("Original pathfinder found no route to the player bed.");
    }

    internal static object Read()
    {
        var friendship = Game1.player.friendshipData["Linus"];
        return new {
            day = Game1.dayOfMonth,
            coconuts = Game1.player.Items.Where(item => item?.QualifiedItemId == "(O)88").Sum(item => item.Stack),
            linus = new { points = friendship.Points, giftsToday = friendship.GiftsToday, giftsThisWeek = friendship.GiftsThisWeek },
            giftsGiven = Game1.stats.GiftsGiven,
            married = Game1.player.isMarriedOrRoommates(), spouse = Game1.player.getSpouse()?.Name,
            houseLevel = Game1.player.HouseUpgradeLevel,
            bedType = Utility.getHomeOfFarmer(Game1.player).GetPlayerBed()?.bedType.ToString(),
            children = Game1.player.getChildren().OrderBy(child => child.Name)
                .Select(child => new { name = child.Name, days = child.daysOld.Value, age = child.Age }).ToArray()
        };
    }

    internal static object Run()
    {
        Random savedRandom = Game1.random;
        try
        {
            Game1.random = new Random(1729);
            var linus = Game1.getCharacterFromName("Linus");
            var friendship = Game1.player.friendshipData["Linus"];
            var coconut = ItemRegistry.Create<StardewValley.Object>("(O)88", quality: 2);
            int points = friendship.Points;
            uint gifts = Game1.stats.GiftsGiven;
            int taste = linus.getGiftTasteForThisItem(coconut);
            linus.receiveGift(coconut, Game1.player, showResponse: false);
            var gift = new { taste, pointsGained = friendship.Points - points, giftsToday = friendship.GiftsToday,
                giftsThisWeek = friendship.GiftsThisWeek, statsGiftsGiven = Game1.stats.GiftsGiven - gifts };
            string[] dialogue = linus.GetGiftReaction(Game1.player, coconut, taste).dialogues.Select(line => line.Text).ToArray();
            bool loaded = linus.TryLoadSchedule();
            var schedule = new { loaded, times = linus.Schedule.OrderBy(pair => pair.Key).Select(pair => new {
                time = pair.Key, location = pair.Value.targetLocationName, x = pair.Value.targetTile.X,
                y = pair.Value.targetTile.Y, facing = pair.Value.facingDirection, steps = pair.Value.route.Count
            }).ToArray() };
            var children = Game1.player.getChildren();
            var family = new { married = Game1.player.isMarriedOrRoommates(), spouse = Game1.player.getSpouse()?.Name,
                children = children.Count };
            var child = children.Single(child => child.Name == "PortChild");
            var stages = new List<object>();
            while (child.daysOld.Value < 55)
            {
                child.dayUpdate(Game1.dayOfMonth);
                if (child.daysOld.Value is 13 or 27 or 55)
                    stages.Add(new { days = child.daysOld.Value, age = child.Age, speed = child.speed });
            }
            return new { gift, dialogue, schedule, family, child = new { stages } };
        }
        finally { Game1.random = savedRandom; }
    }
}
