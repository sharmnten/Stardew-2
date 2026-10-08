using Microsoft.Xna.Framework;
using System.Reflection;
using StardewValley;
using StardewValley.Locations;

namespace StardewBrowser.Testing;

internal static class MovieActions
{
    internal const string FixtureKey = "StardewBrowser.MovieFixture";
    internal static int State => (int)typeof(MovieTheater).GetProperty("CurrentState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Theater)!;
    internal static MovieTheater Theater => Game1.RequireLocation<MovieTheater>("MovieTheater");

    internal static void Prepare()
    {
        Game1.player.modData[FixtureKey] = "1";
        Game1.year = 2; Game1.season = Season.Spring; Game1.dayOfMonth = 5;
        Game1.stats.DaysPlayed = 117;
        Game1.player.mailReceived.Add("gotPet");
        Game1.player.mailReceived.Add("sawQiPlane");
        Game1.player.mailReceived.Add("ccMovieTheater");
        Game1.player.eventsSeen.Add("15389722");
        Game1.player.Items[0] = ItemRegistry.Create("(O)809", 2);
        Game1.player.friendshipData["Linus"] = new Friendship(750);
        Game1.netWorldState.Value.UpdateFromGame1();
    }

    internal static Point ActionTile(GameLocation location, string action)
    {
        var layer = location.Map.Layers[0];
        for (int y = 0; y < layer.LayerHeight; y++)
        for (int x = 0; x < layer.LayerWidth; x++)
            if (location.doesTileHaveProperty(x, y, "Action", "Buildings") == action) return new Point(x, y);
        throw new InvalidOperationException("Original map is missing " + action);
    }

    internal static void Invite()
    {
        Game1.player.CurrentToolIndex = 0;
        if (!Game1.getCharacterFromName("Linus").checkAction(Game1.player, Game1.currentLocation))
            throw new InvalidOperationException("Original ticket invitation did not start.");
    }

    internal static object Read() => new {
        day = Game1.dayOfMonth,
        tickets = Game1.player.Items.Where(item => item?.QualifiedItemId == "(O)809").Sum(item => item.Stack),
        friendship = Game1.player.friendshipData["Linus"].Points,
        invited = Game1.player.team.movieInvitations.Where(invitation => invitation.farmer == Game1.player)
            .Select(invitation => invitation.invitedNPC.Name).Order().ToArray(),
        viewedWeek = Game1.player.lastSeenMovieWeek.Value,
        guestViewedWeek = Game1.getCharacterFromName("Linus").lastSeenMovieWeek.Value,
        theaterState = State,
        movie = MovieTheater.GetMovieToday().Id,
        response = MovieTheater.GetResponseForMovie(Game1.getCharacterFromName("Linus"))
    };
}
