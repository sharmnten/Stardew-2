using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace StardewBrowser.Testing;

internal static class AdvancedActions
{
    internal const string FixtureKey = "StardewBrowser.Testing/advanced-reference";
    private static readonly Vector2 HouseMachineTile = new(4, 7);
    internal static void Prepare()
    {
        FarmingActions.Prepare();
        EconomyActions.Prepare();
        ProductionActions.Prepare();
        Game1.player.Items[2] = ItemRegistry.Create("(O)24", 20);
        Game1.player.friendshipData["Linus"] = new Friendship(750);
        Game1.player.professions.Add(12);
        Game1.player.professions.Add(14);
        Game1.player.foragingLevel.Value = 10;
        Game1.player.experiencePoints[2] = 15000;
        Game1.player.mailReceived.Add("gotPet");
        Game1.player.modData[FixtureKey] = "1";
        var farm = Game1.getFarm();
        farm.getShippingBin(Game1.player).Add(ItemRegistry.Create("(O)24", 3));
        farm.getShippingBin(Game1.player).Add(ItemRegistry.Create("(O)24", 1, quality: 2));
        var machine = farm.objects[new Vector2(25, 20)];
        // Indoor placement prevents original seasonal weeds from destroying this prerequisite.
        farm.objects.Remove(new Vector2(25, 20));
        var house = (FarmHouse)Game1.getLocationFromName("FarmHouse");
        machine.TileLocation = HouseMachineTile;
        machine.Location = house;
        house.objects[HouseMachineTile] = machine;
        if (!machine.performObjectDropInAction(Game1.player.Items[1], false, Game1.player))
            throw new InvalidOperationException("Original machine did not accept the prerequisite egg.");
    }

    internal static void BeginSleep()
    {
        var house = (FarmHouse)Game1.currentLocation;
        Game1.player.Position = house.GetPlayerBedSpot().ToVector2() * 64;
        Game1.player.isInBed.Value = true;
        if (!house.answerDialogueAction("Sleep_Yes", null)) throw new InvalidOperationException("Original sleep action unavailable.");
    }

    internal static object Read()
    {
        var farm = Game1.getFarm();
        var greenhouse = farm.buildings.Single(building => building.buildingType.Value == "Greenhouse").GetIndoors();
        var soil = (HoeDirt)farm.terrainFeatures[new Vector2(20, 20)];
        var indoor = (HoeDirt)greenhouse.terrainFeatures[new Vector2(4, 4)];
        var machine = Game1.getLocationFromName("FarmHouse").objects[HouseMachineTile];
        var chest = (Chest)farm.objects[new Vector2(24, 20)];
        var friendship = Game1.player.friendshipData["Linus"];
        return new {
            day = Game1.dayOfMonth, season = Game1.season.ToString(), year = Game1.year, money = Game1.player.Money,
            weather = new { raining = Game1.isRaining, snowing = Game1.isSnowing, lightning = Game1.isLightning,
                tomorrow = Game1.weatherForTomorrow },
            qiGems = Game1.player.QiGems, casinoCoins = Game1.player.clubCoins,
            inventory = Game1.player.Items.Where(item => item != null)
                .Select(item => new { id = item.QualifiedItemId, quality = item.Quality, stack = item.Stack }).ToArray(),
            chest = chest.GetItemsForPlayer().Select(item => new { id = item.QualifiedItemId, quality = item.Quality, stack = item.Stack }).ToArray(),
            crops = new { outdoorDead = soil.crop.dead.Value, greenhouseDead = indoor.crop.dead.Value,
                greenhousePhase = indoor.crop.currentPhase.Value, greenhouseDays = indoor.crop.dayOfCurrentPhase.Value },
            machine = new { output = machine.heldObject.Value?.QualifiedItemId, minutes = machine.MinutesUntilReady, ready = machine.readyForHarvest.Value },
            friendship = new { points = friendship.Points, gifts = friendship.GiftsThisWeek },
            professions = Game1.player.professions.Order().ToArray(),
            foragingXp = Game1.player.experiencePoints[2],
            recipes = new { chest = Game1.player.craftingRecipes.ContainsKey("Chest"), egg = Game1.player.cookingRecipes.ContainsKey("Fried Egg") },
            receivedPetMail = Game1.player.mailReceived.Contains("gotPet")
        };
    }
}
