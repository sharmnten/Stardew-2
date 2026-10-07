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
    internal static void Prepare()
    {
        for (int i = 0; i < Game1.player.Items.Count; i++) Game1.player.Items[i] = null;
        Game1.player.Items[0] = ItemRegistry.Create<FishingRod>("(T)IridiumRod");
        Game1.player.Items[1] = ItemRegistry.Create("(O)685", 2);
        Game1.player.CurrentToolIndex = 0;
        Game1.player.fishingLevel.Value = 5;
        Game1.player.experiencePoints[1] = 2150;
        var beach = Game1.getLocationFromName("Beach");
        var pot = new CrabPot();
        bool placed = false;
        for (int y = 1; y < beach.Map.Layers[0].LayerHeight - 1 && !placed; y++)
        for (int x = 1; x < beach.Map.Layers[0].LayerWidth - 1 && !placed; x++)
            if (CrabPot.IsValidCrabPotLocationTile(beach, x, y))
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

    private static int CountBait() => Game1.player.Items.Where(item => item?.QualifiedItemId == "(O)685").Sum(item => item.Stack);
}
