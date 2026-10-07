using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewBrowser.Testing;

internal static class MuseumActions
{
    internal const string FixtureKey = "StardewBrowser.MuseumFixture";

    internal static void Prepare()
    {
        Game1.player.modData[FixtureKey] = "1";
        Game1.year = 2; Game1.season = Season.Spring; Game1.dayOfMonth = 5;
        Game1.stats.DaysPlayed = 117;
        Game1.player.eventsSeen.Add("0"); // The original museum introduction has already been seen.
        Game1.player.addQuest("24");
        Game1.player.Items[0] = ItemRegistry.Create("(O)86");
        Game1.netWorldState.Value.UpdateFromGame1();
    }

    internal static LibraryMuseum Museum => Game1.RequireLocation<LibraryMuseum>("ArchaeologyHouse");
    internal static void Open() => Museum.OpenDonationMenu();

    internal static void Donate()
    {
        var menu = (MuseumMenu)Game1.activeClickableMenu;
        var item = menu.inventory.inventory[0].bounds.Center;
        menu.receiveLeftClick(item.X, item.Y);
        var spot = Museum.getFreeDonationSpot();
        menu.receiveLeftClick((int)spot.X * 64 + 32 - Game1.viewport.X, (int)spot.Y * 64 + 32 - Game1.viewport.Y);
    }

    internal static void Close()
    {
        var menu = (MuseumMenu)Game1.activeClickableMenu;
        var button = menu.okButton.bounds.Center;
        menu.receiveLeftClick(button.X, button.Y);
    }

    internal static object Read()
    {
        var quest = Game1.player.questLog.Single(quest => quest.id.Value == "24");
        return new {
            day = Game1.dayOfMonth,
            crystals = Game1.player.Items.Where(item => item?.QualifiedItemId == "(O)86").Sum(item => item.Stack),
            pieces = Museum.museumPieces.Pairs.OrderBy(piece => piece.Key.X).ThenBy(piece => piece.Key.Y)
                .Select(piece => new { x = (int)piece.Key.X, y = (int)piece.Key.Y, id = piece.Value }).ToArray(),
            quest = new { id = quest.id.Value, completed = quest.completed.Value, moneyReward = quest.moneyReward.Value }
        };
    }
}
