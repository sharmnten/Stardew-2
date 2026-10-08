using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;
using NativeObject = StardewValley.Object;

namespace StardewBrowser.Testing;

internal static class FishingActions
{
    internal const string FixtureKey = "StardewBrowser.FishingFixture";

    internal static void Prepare()
    {
        Game1.player.modData[FixtureKey] = "1";
        for (int i = 0; i < Game1.player.Items.Count; i++) Game1.player.Items[i] = null;
        Game1.player.Items[0] = ItemRegistry.Create<FishingRod>("(T)IridiumRod");
        Game1.player.Items[1] = ItemRegistry.Create("(O)685", 2);
        Game1.player.CurrentToolIndex = 0;
        Game1.player.fishingLevel.Value = 5;
        Game1.player.experiencePoints[1] = 2150;
        Game1.player.professions.Add(6); // This level-5 reference farmer has already selected Fisher.
        var beach = Game1.getLocationFromName("Beach");
        var pot = new CrabPot();
        bool placed = false;
        for (int y = 1; y < beach.Map.Layers[0].LayerHeight - 1 && !placed; y++)
        for (int x = 1; x < beach.Map.Layers[0].LayerWidth - 1 && !placed; x++)
            if (CrabPot.IsValidCrabPotLocationTile(beach, x, y) && FindShore(beach, new Vector2(x, y)) != null)
                placed = pot.placementAction(beach, x * 64, y * 64, Game1.player);
        if (!placed) throw new InvalidOperationException("Original beach has no valid trap site.");
        var pond = new FishPond(new Vector2(35, 35));
        Game1.getFarm().buildings.Add(pond);
        pond.FinishConstruction();
        pond.fishType.Value = "145";
        pond.currentOccupants.Value = 1;
        pond.UpdateMaximumOccupancy();
        pond.daysSinceSpawn.Value = pond.GetFishPondData().SpawnTime - 1;
    }

    internal static object Run()
    {
        var rod = (FishingRod)Game1.player.Items[0];
        rod.attach(ItemRegistry.Create<NativeObject>("(O)685", 20));
        rod.attach(ItemRegistry.Create<NativeObject>("(O)686"));
        var attachment = new { bait = rod.GetBait()?.QualifiedItemId, baitStack = rod.GetBait()?.Stack,
            tackle = rod.GetTackleQualifiedItemIDs() };
        var pot = Game1.getLocationFromName("Beach").objects.Values.OfType<CrabPot>().Single();
        int before = CountBait();
        bool accepted = pot.AttemptAutoLoad(Game1.player.Items, Game1.player);
        int inputConsumed = before - CountBait();
        pot.DayUpdate();
        var trap = new { accepted, inputConsumed, ready = pot.readyForHarvest.Value,
            output = pot.heldObject.Value?.QualifiedItemId, quality = pot.heldObject.Value?.Quality };
        var pond = Game1.getFarm().buildings.OfType<FishPond>().Single();
        int population = pond.FishCount;
        pond.dayUpdate(Game1.dayOfMonth);
        var pondResult = new { before = population, after = pond.FishCount, days = pond.daysSinceSpawn.Value,
            capacity = pond.maxOccupants.Value, output = pond.output.Value?.QualifiedItemId };
        Random savedRandom = Game1.random;
        object physics;
        try
        {
            Game1.random = new Random(1729);
            var bar = new BobberBar("145", 0.5f, false, [], null, false);
            var initial = new { height = bar.bobberBarHeight, difficulty = bar.difficulty, size = bar.fishSize };
            for (int frame = 1; frame <= 32; frame++)
                bar.update(new GameTime(TimeSpan.FromMilliseconds(frame * 16), TimeSpan.FromMilliseconds(16)));
            physics = new { initial, after = new { scale = bar.scale, position = Math.Round(bar.bobberPosition, 4),
                barPosition = Math.Round(bar.bobberBarPos, 4), distance = Math.Round(bar.distanceFromCatching, 4) } };
            bar.emergencyShutDown();
        }
        finally { Game1.random = savedRandom; }
        return new { rod = attachment, pot = trap, pond = pondResult, bar = physics };
    }

    internal static CrabPot Pot => Game1.getLocationFromName("Beach").objects.Values.OfType<CrabPot>().Single();
    internal static object GatherAndRegenerate()
    {
        var beach = Game1.currentLocation;
        if (beach.Name != "Beach") throw new InvalidOperationException("Visit the original beach before gathering.");
        Random savedRandom = Game1.random;
        try
        {
            // Beach.DayUpdate also uses the live random stream, unlike the
            // save-seeded base forage pass. Control that input for both hosts.
            Game1.random = new Random(1729);
            // Exercise two weekly resource boundaries, after the persisted trap/
            // pond checks. The original DayUpdate removes old forage and respawns it.
            Game1.dayOfMonth = 7; Game1.stats.DaysPlayed = 7;
            Game1.netWorldState.Value.UpdateFromGame1();
            int firstSeed = SpawnWeek(7);
            var spawned = Forage();
            var target = beach.objects.Pairs.Where(pair => pair.Value.IsSpawnedObject && pair.Value.isForage())
                .OrderBy(pair => pair.Key.X).ThenBy(pair => pair.Key.Y).First();
            string id = target.Value.QualifiedItemId;
            Game1.player.Position = new Vector2(target.Key.X * 64 - 64, target.Key.Y * 64);
            uint before = Game1.stats.ItemsForaged;
            int owned = Count();
            bool harvested = beach.checkAction(new xTile.Dimensions.Location((int)target.Key.X, (int)target.Key.Y),
                Game1.viewport, Game1.player);
            bool removed = !beach.objects.ContainsKey(target.Key);
            int added = Count() - owned;
            uint foraged = Game1.stats.ItemsForaged - before;
            Game1.dayOfMonth = 14; Game1.stats.DaysPlayed = 14;
            Game1.netWorldState.Value.UpdateFromGame1();
            int secondSeed = SpawnWeek(14);
            var regenerated = Forage();
            return new { spawned = spawned.Length, harvested, removed, id, inventoryAdded = added,
                itemsForaged = foraged, regenerated = regenerated.Length, first = spawned, second = regenerated, firstSeed, secondSeed };

            int SpawnWeek(int day)
            {
                // Original weekly spawning can legitimately yield no accessible
                // forage. Select a reproducible input, never inject a drop.
                for (int seed = 1729; seed < 1793; seed++)
                {
                    Game1.random = new Random(seed);
                    beach.DayUpdate(day);
                    if (Forage().Length > 0) return seed;
                }
                throw new InvalidOperationException("No original beach forage spawned across 64 deterministic inputs.");
            }

            int Count() => Game1.player.Items.Where(item => item?.QualifiedItemId == id).Sum(item => item.Stack);
            object[] Forage() => beach.objects.Pairs.Where(pair => pair.Value.IsSpawnedObject && pair.Value.isForage())
                .OrderBy(pair => pair.Key.X).ThenBy(pair => pair.Key.Y)
                .Select(pair => (object)new { x = pair.Key.X, y = pair.Key.Y, id = pair.Value.QualifiedItemId }).ToArray();
        }
        finally { Game1.random = savedRandom; }
    }

    internal static FishPond Pond => Game1.getFarm().buildings.OfType<FishPond>().Single();

    internal static (Vector2 Shore, Vector2 Target) PondSpot()
    {
        var pond = Pond;
        var farm = Game1.getFarm();
        for (int x = pond.tileX.Value; x < pond.tileX.Value + pond.tilesWide.Value; x++)
        foreach (bool above in new[] { true, false })
        {
            int y = above ? pond.tileY.Value - 1 : pond.tileY.Value + pond.tilesHigh.Value;
            var shore = new Vector2(x, y);
            if (!farm.isWaterTile(x, y) && farm.isTilePassable(shore)
                && !farm.objects.ContainsKey(shore) && !farm.terrainFeatures.ContainsKey(shore))
                return (shore, new Vector2(x, above ? y + 1 : y - 1));
        }
        throw new InvalidOperationException("The original pond needs an adjacent passable collection tile.");
    }

    internal static void HarvestPond()
    {
        Game1.player.CurrentToolIndex = 0;
        if (Pond.output.Value == null || !Pond.doAction(PondSpot().Target, Game1.player))
            throw new InvalidOperationException("Original pond produce harvest was rejected.");
    }
    internal static Vector2 ShoreSpot() => FindShore(Game1.getLocationFromName("Beach"), Pot.TileLocation)
        ?? throw new InvalidOperationException("The original trap needs an adjacent passable shore tile.");

    private static Vector2? FindShore(GameLocation beach, Vector2 tile)
    {
        foreach (var offset in new[] { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(0, -1) })
        {
            var shore = tile + offset;
            if (shore.X > 0 && shore.Y > 0 && shore.X < beach.Map.Layers[0].LayerWidth - 1
                && shore.Y < beach.Map.Layers[0].LayerHeight - 1 && !beach.isWaterTile((int)shore.X, (int)shore.Y)
                && beach.isTilePassable(shore) && !beach.objects.ContainsKey(shore)
                && !beach.terrainFeatures.ContainsKey(shore)) return shore;
        }
        return null;
    }

    internal static void Harvest()
    {
        if (!Pot.checkForAction(Game1.player)) throw new InvalidOperationException("Original crab pot harvest was rejected.");
    }

    internal static void Rebait()
    {
        if (!Pot.AttemptAutoLoad(Game1.player.Items, Game1.player))
            throw new InvalidOperationException("Original crab pot rebait was rejected.");
    }

    internal static object Read()
    {
        var pot = Pot;
        var pond = Game1.getFarm().buildings.OfType<FishPond>().Single();
        return new {
            day = Game1.dayOfMonth, experience = Game1.player.experiencePoints[1], baitCount = CountBait(),
            professions = Game1.player.professions.Order().ToArray(),
            pot = new { x = (int)pot.TileLocation.X, y = (int)pot.TileLocation.Y,
                bait = pot.bait.Value?.QualifiedItemId, ready = pot.readyForHarvest.Value,
                output = pot.heldObject.Value?.QualifiedItemId, quality = pot.heldObject.Value?.Quality },
            @catch = Game1.player.Items.Where(item => item != null && item is not FishingRod && item.QualifiedItemId != "(O)685")
                .Select(item => new { id = item.QualifiedItemId, stack = item.Stack, quality = item.Quality }).ToArray(),
            roe = Game1.player.Items.OfType<NativeObject>().Where(item => item.QualifiedItemId == "(O)812")
                .Select(item => new { parent = item.preservedParentSheetIndex.Value,
                    preserve = item.preserve.Value.ToString(), price = item.Price }).ToArray(),
            pond = new { population = pond.FishCount, days = pond.daysSinceSpawn.Value, output = pond.output.Value?.QualifiedItemId }
        };
    }

    private static int CountBait() => Game1.player.Items.Where(item => item?.QualifiedItemId == "(O)685").Sum(item => item.Stack);
}
