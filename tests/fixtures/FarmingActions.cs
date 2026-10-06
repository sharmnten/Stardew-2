using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewBrowser.Testing;

/// <summary>Shared test driver; both runtimes execute their own original crop methods.</summary>
internal static class FarmingActions
{
    private static readonly Vector2 WetTile = new(20, 20), DryTile = new(21, 20), HouseTile = new(4, 4);
    private static readonly Vector2 ToolTile = new(22, 20), WildTile = new(30, 20), FruitTile = new(30, 30);

    internal static void Prepare()
    {
        // Prerequisites, serialized by the original desktop serializer before testing.
        Game1.year = 2; Game1.season = Season.Spring; Game1.dayOfMonth = 28;
        Game1.stats.DaysPlayed = 140;
        Game1.netWorldState.Value.UpdateFromGame1();
        var farm = Game1.getFarm();
        var greenhouse = farm.buildings.Single(building => building.buildingType.Value == "Greenhouse").GetIndoors();
        Plant(farm, WetTile, 1); Plant(farm, DryTile, 0); Plant(greenhouse, HouseTile, 1);
        foreach (var center in new[] { ToolTile, WildTile, FruitTile })
        {
            int radius = center == ToolTile ? 0 : 1;
            for (int x = (int)center.X - radius; x <= center.X + radius; x++)
            for (int y = (int)center.Y - radius; y <= center.Y + radius; y++)
            {
                farm.objects.Remove(new Vector2(x, y));
                farm.terrainFeatures.Remove(new Vector2(x, y));
            }
            var area = new Rectangle(((int)center.X - 1) * 64, ((int)center.Y - 1) * 64, 192, 192);
            foreach (var clump in farm.resourceClumps.ToArray())
                if (clump.getBoundingBox().Intersects(area)) farm.resourceClumps.Remove(clump);
        }
        var tree = new Tree("1", 0);
        tree.fertilized.Value = true;
        farm.terrainFeatures[WildTile] = tree;
        farm.terrainFeatures[FruitTile] = new FruitTree("628");
    }

    private static void Plant(GameLocation location, Vector2 tile, int water)
    {
        var soil = new HoeDirt(water, location) { crop = new Crop("472", (int)tile.X, (int)tile.Y, location) };
        location.terrainFeatures[tile] = soil;
    }

    internal static object Run()
    {
        var farm = Game1.getFarm();
        var greenhouse = farm.buildings.Single(building => building.buildingType.Value == "Greenhouse").GetIndoors();
        var wet = (HoeDirt)farm.terrainFeatures[WetTile];
        var dry = (HoeDirt)farm.terrainFeatures[DryTile];
        var indoor = (HoeDirt)greenhouse.terrainFeatures[HouseTile];
        float stamina = Game1.player.Stamina;
        Game1.player.toolPower.Value = 0;
        new Hoe().DoFunction(farm, (int)ToolTile.X * 64, (int)ToolTile.Y * 64, 1, Game1.player);
        bool hoed = farm.terrainFeatures.TryGetValue(ToolTile, out var tilled) && tilled is HoeDirt;
        var watering = new WateringCan();
        watering.WaterLeft = 40;
        watering.DoFunction(farm, (int)ToolTile.X * 64, (int)ToolTile.Y * 64, 1, Game1.player);
        var tools = new { hoed, waterState = (tilled as HoeDirt)?.state.Value, waterLeft = watering.WaterLeft,
            staminaUsed = stamina - Game1.player.Stamina };
        var wild = (Tree)farm.terrainFeatures[WildTile];
        var fruit = (FruitTree)farm.terrainFeatures[FruitTile];
        wild.dayUpdate(); fruit.dayUpdate();
        var trees = new { wildStage = wild.growthStage.Value, fruitDays = fruit.daysUntilMature.Value };
        wet.dayUpdate(); dry.dayUpdate();
        var wateredGrowth = new { phase = wet.crop.currentPhase.Value, days = wet.crop.dayOfCurrentPhase.Value };
        var dryGrowth = new { phase = dry.crop.currentPhase.Value, days = dry.crop.dayOfCurrentPhase.Value };
        for (int day = 0; day < 3; day++) { wet.state.Value = 1; wet.dayUpdate(); }
        int before = CountParsnips();
        wet.crop.harvest((int)WetTile.X, (int)WetTile.Y, wet);
        int harvestedParsnips = CountParsnips() - before;
        Game1.season = Season.Summer; Game1.dayOfMonth = 1;
        Game1.netWorldState.Value.UpdateFromGame1();
        dry.state.Value = 1; indoor.state.Value = 1;
        dry.dayUpdate(); indoor.dayUpdate();
        return new { tools, trees, wateredGrowth, dryGrowth, harvestedParsnips,
            summer = new { outdoorDead = dry.crop.dead.Value, greenhouseDead = indoor.crop.dead.Value,
                greenhousePhase = indoor.crop.currentPhase.Value, greenhouseDays = indoor.crop.dayOfCurrentPhase.Value } };
    }

    private static int CountParsnips() => Game1.player.Items.Where(item => item?.QualifiedItemId == "(O)24").Sum(item => item.Stack);
}
