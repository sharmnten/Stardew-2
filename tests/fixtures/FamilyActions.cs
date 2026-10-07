using StardewValley;
using StardewValley.Characters;

namespace StardewBrowser.Testing;

internal static class FamilyActions
{
    internal static void Prepare()
    {
        Game1.year = 2; Game1.season = Season.Spring; Game1.dayOfMonth = 5;
        Game1.stats.DaysPlayed = 117;
        Game1.player.friendshipData["Linus"] = new Friendship(750);
        Game1.player.spouse = "Abigail";
        Game1.player.friendshipData["Abigail"] = new Friendship(2750) {
            Status = FriendshipStatus.Married, WeddingDate = new WorldDate(1, "spring", 1)
        };
        Game1.player.HouseUpgradeLevel = 2;
        var home = Utility.getHomeOfFarmer(Game1.player);
        home.setMapForUpgradeLevel(2);
        var child = new Child("PortChild", true, false, Game1.player) { currentLocation = home, Age = 0 };
        child.daysOld.Value = 12;
        home.characters.Add(child);
        Game1.netWorldState.Value.UpdateFromGame1();
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
