using StardewValley;
using StardewValley.Locations;
using System.Security.Cryptography;
using System.Text;

namespace StardewBrowser.Testing;

internal static class CalendarActions
{
    internal static void Prepare()
    {
        Game1.year = 2; Game1.season = Season.Spring; Game1.dayOfMonth = 13;
        Game1.stats.DaysPlayed = 125;
        Game1.player.friendshipData["Linus"] = new Friendship(750);
        Game1.netWorldState.Value.UpdateFromGame1();
    }

    internal static void BeginFestival()
    {
        Game1.timeOfDay = 900;
        Game1.warpFarmer("Town", 23, 9, false);
    }

    internal static bool FestivalReady() => Game1.currentLocation.currentEvent is {
        isFestival: true, playerControlSequence: true } && !Game1.isWarping && Game1.player.CanMove;

    internal static object Run()
    {
        var ev = Game1.currentLocation.currentEvent;
        var festival = new { id = ev.id, name = ev.FestivalName, freeMovement = ev.playerControlSequence,
            actors = ev.actors.Select(actor => actor.Name).Order().ToArray(),
            map = Game1.currentLocation.mapPath.Value,
            tileSheets = Game1.currentLocation.Map.TileSheets.Select(sheet => sheet.Id).Order().ToArray() };
        var calendar = DataLoader.Festivals_FestivalDates(Game1.content).Keys.Order().Select(id => {
            bool loaded = Event.tryToLoadFestivalData(id, out var asset, out var data, out var location,
                out int start, out int end);
            string script = loaded ? data["set-up"] : "";
            return new { id, loaded, asset, location, start, end, scriptLength = script.Length,
                scriptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script))) };
        }).ToArray();
        Season season = Game1.season;
        int day = Game1.dayOfMonth;
        object birthday;
        try
        {
            Game1.season = Season.Winter; Game1.dayOfMonth = 3;
            var linus = Game1.getCharacterFromName("Linus");
            int points = Game1.player.friendshipData["Linus"].Points;
            bool isBirthday = linus.isBirthday();
            linus.receiveGift(ItemRegistry.Create<StardewValley.Object>("(O)88"), Game1.player, showResponse: false);
            birthday = new { isBirthday, pointsGained = Game1.player.friendshipData["Linus"].Points - points };
        }
        finally { Game1.season = season; Game1.dayOfMonth = day; }
        bool available = Utility.TryGetPassiveFestivalDataForDay(16, Season.Winter, null, out string passiveId, out var passiveData);
        var passive = new { available, id = passiveId, start = passiveData?.StartDay, end = passiveData?.EndDay };
        var movies = new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter }.Select(season => {
            var movie = MovieTheater.GetMovieForDate(new WorldDate(2, season, 1));
            return new { season = season.ToString(), id = movie.Id, title = movie.Title };
        }).ToArray();
        return new { festival, calendar, birthday, passive, movies };
    }
}
