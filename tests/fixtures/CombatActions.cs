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
    internal static void Prepare()
    {
        Game1.year = 2; Game1.season = Season.Spring; Game1.dayOfMonth = 5;
        Game1.stats.DaysPlayed = 117;
        Game1.player.hasSkullKey = true;
        Game1.player.mailReceived.Add("Island_Turtle");
        Game1.netWorldState.Value.UpdateFromGame1();
    }

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
