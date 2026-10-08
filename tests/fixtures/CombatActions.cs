using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Tools;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;

namespace StardewBrowser.Testing;

internal static class CombatActions
{
    internal const string FixtureKey = "StardewBrowser.CombatFixture";
    private static GreenSlime? target;
    private static uint startingKills;
    private static int startingExperience;
    internal static void Prepare()
    {
        Game1.player.modData[FixtureKey] = "1";
        Game1.year = 2; Game1.season = Season.Spring; Game1.dayOfMonth = 5;
        Game1.stats.DaysPlayed = 117;
        Game1.player.hasSkullKey = true;
        Game1.player.mailReceived.Add("Island_Turtle");
        Game1.player.mailReceived.Add("sawQiPlane");
        Game1.player.Items[0] = new MeleeWeapon("0");
        Game1.netWorldState.Value.UpdateFromGame1();
    }

    internal static object StartLiveEncounter()
    {
        if (Game1.currentLocation is not MineShaft mine || mine.mineLevel != 5)
            throw new InvalidOperationException("Enter original mine floor 5 first.");
        startingKills = Game1.stats.SlimesKilled;
        startingExperience = Game1.player.experiencePoints[4];
        Game1.player.CurrentToolIndex = 0;
        Game1.player.faceDirection(1);
        // An injured, stationary monster is an encounter prerequisite. Damage,
        // removal, XP, kill counters and loot must come from the original swing.
        target = new GreenSlime(Game1.player.Position + new Vector2(64, 0), 5) {
            currentLocation = mine, Health = 1, speed = 0, timeBeforeAIMovementAgain = 60000
        };
        target.resilience.Value = 0;
        target.objectsToDrop.Add("766");
        mine.characters.Add(target);
        return new { x = target.GetBoundingBox().Center.X, y = target.GetBoundingBox().Center.Y };
    }

    internal static object ReadLive() => new {
        location = Game1.currentLocation.Name, deepest = Game1.player.deepestMineLevel,
        slimesKilled = Game1.stats.SlimesKilled - startingKills,
        experience = Game1.player.experiencePoints[4] - startingExperience,
        killed = target?.Health <= 0,
        loot = Game1.currentLocation.debris.Any(debris => debris.itemId.Value is "766" or "(O)766"
            || debris.item?.QualifiedItemId == "(O)766")
            || Game1.player.Items.Any(item => item?.QualifiedItemId == "(O)766")
    };

    internal static object ReadSaved() => new { day = Game1.dayOfMonth,
        deepest = Game1.player.deepestMineLevel, slimesKilled = Game1.stats.SlimesKilled,
        experience = Game1.player.experiencePoints[4], weapon = Game1.player.Items[0]?.QualifiedItemId };

    internal static object Run()
    {
        Random savedRandom = Game1.random;
        try
        {
            Game1.random = new Random(1729);
            var mine = GenerateMine(5);
            var cavern = GenerateMine(121);
            var volcano = VolcanoDungeon.GetLevel("VolcanoDungeon1");
            object[] dungeons = [ReadDungeon(mine), ReadDungeon(cavern), ReadDungeon(volcano)];
            var weapon = new MeleeWeapon("0");
            var weaponResult = new { id = weapon.QualifiedItemId, minDamage = weapon.minDamage.Value,
                maxDamage = weapon.maxDamage.Value };
            var slime = new GreenSlime(new Vector2(10 * 64, 10 * 64), 5) { currentLocation = mine };
            int health = slime.Health;
            int damage = slime.takeDamage(1, 0, 0, false, 1, Game1.player);
            int healthLost = health - slime.Health;
            uint kills = Game1.stats.SlimesKilled;
            slime.takeDamage(500, 0, 0, false, 1, Game1.player);
            var monster = new { damage, healthLost, killed = slime.Health <= 0,
                slimesKilled = Game1.stats.SlimesKilled - kills, dropTable = slime.objectsToDrop.ToArray() };
            var effects = new BuffEffects();
            effects.Speed.Value = 2;
            effects.Defense.Value = 3;
            const string id = "StardewBrowser.Testing/combat";
            Game1.player.buffs.Apply(new Buff(id, duration: 100, effects: effects, displayName: "Reference buff"));
            bool applied = Game1.player.buffs.IsApplied(id);
            float speed = Game1.player.buffs.Speed;
            int defense = Game1.player.buffs.Defense;
            Game1.player.buffs.Update(new GameTime(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200)));
            var buff = new { applied, speed, defense, expired = !Game1.player.buffs.IsApplied(id) };
            return new { dungeons, weapon = weaponResult, monster, buff };
        }
        finally { Game1.random = savedRandom; }
    }

    private static MineShaft GenerateMine(int level)
    {
        var mine = new MineShaft(level) { mineRandom = new Random(1729 + level) };
        MineShaft.activeMines.Add(mine);
        typeof(MineShaft).GetMethod("generateContents", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(mine, null);
        return mine;
    }

    private static object ReadDungeon(GameLocation location)
    {
        string objects = string.Join(";", location.objects.Pairs.OrderBy(pair => pair.Key.X).ThenBy(pair => pair.Key.Y)
            .Select(pair => $"{pair.Key.X},{pair.Key.Y}:{pair.Value.QualifiedItemId}"));
        return new { name = location.Name, width = location.Map.Layers[0].LayerWidth,
            height = location.Map.Layers[0].LayerHeight,
            tileSheets = location.Map.TileSheets.Select(sheet => sheet.Id).Order().ToArray(),
            objects = location.objects.Count(), objectHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(objects))),
            monsters = location.characters.OfType<Monster>().GroupBy(monster => monster.GetType().Name)
                .Select(group => new { type = group.Key, count = group.Count() }).OrderBy(group => group.type).ToArray() };
    }
}
