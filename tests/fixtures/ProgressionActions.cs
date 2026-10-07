using StardewValley;
using StardewValley.Constants;
using StardewValley.Menus;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace StardewBrowser.Testing;

internal static class ProgressionActions
{
    internal const string FixtureKey = "StardewBrowser.ProgressionFixture";
    private static readonly string[] MasteryRecipes = ["Statue Of Blessings", "Challenge Bait", "Mystic Tree Seed",
        "Treasure Totem", "Heavy Furnace", "Statue Of The Dwarf King", "Anvil", "Mini-Forge"];

    internal static void Prepare()
    {
        Game1.player.modData[FixtureKey] = "1";
        Game1.player.totalMoneyEarned = 14999;
        Game1.player.achievements.Clear();
        Game1.player.fishCaught.Clear();
    }

    internal static object Run()
    {
        for (int skill = 0; skill < 5; skill++) Game1.player.gainExperience(skill, 15000);
        var skills = new { levels = Enumerable.Range(0, 5).Select(Game1.player.GetUnmodifiedSkillLevel).ToArray(),
            pendingLevels = Game1.player.newLevels.Count };
        Game1.player.gainExperience(0, 1000);
        Game1.player.gainExperience(2, 9500);
        var menu = new MasteryTrackerMenu(3);
        var button = menu.mainButton.bounds;
        menu.receiveLeftClick(button.Center.X, button.Center.Y);
        var mastery = new { experience = Game1.stats.Get(StatKeys.MasteryExp), level = MasteryTrackerMenu.getCurrentMasteryLevel(),
            claimed = Game1.player.stats.Get(StatKeys.Mastery(3)), spent = Game1.stats.Get(StatKeys.MasteryLevelsSpent),
            heavyFurnaceRecipe = Game1.player.craftingRecipes.ContainsKey("Heavy Furnace"),
            dwarfStatueRecipe = Game1.player.craftingRecipes.ContainsKey("Statue Of The Dwarf King") };
        Game1.player.Money += 1;
        Game1.stats.checkForMoneyAchievements();
        Game1.player.caughtFish("(O)145", 20);
        var collection = new { fishRegistered = Game1.player.fishCaught.ContainsKey("(O)145"),
            fish = Game1.player.fishCaught["(O)145"] };
        return new { skills, mastery, achievements = Game1.player.achievements.Order().ToArray(), collection };
    }

    internal static void ShowProfession(int skill, int level) => Game1.activeClickableMenu = new LevelUpMenu(skill, level);

    internal static void EarnRemainingMastery() => Game1.player.gainExperience(2, 90000);

    internal static void ShowMastery(int skill)
    {
        string[] skills = ["Farming", "Fishing", "Foraging", "Mining", "Combat"];
        string action = "MasteryCave_" + skills[skill];
        var location = Game1.currentLocation;
        if (location.Name != "MasteryCave") throw new InvalidOperationException("Warp to the original mastery cave first.");
        var layer = location.Map.GetLayer("Buildings");
        for (int y = 0; y < layer.LayerHeight; y++)
        for (int x = 0; x < layer.LayerWidth; x++)
        {
            var tile = layer.Tiles[x, y];
            if (tile?.Properties.TryGetValue("Action", out var value) == true && value.ToString() == action)
            {
                if (!location.performAction(action, Game1.player, new xTile.Dimensions.Location(x, y)))
                    throw new InvalidOperationException("Original mastery plaque action failed.");
                return;
            }
        }
        throw new InvalidOperationException("Original mastery plaque was not found: " + action);
    }

    internal static void ClaimMastery(int skill)
    {
        ShowMastery(skill);
        var menu = (MasteryTrackerMenu)Game1.activeClickableMenu;
        var point = menu.mainButton.bounds.Center;
        menu.receiveLeftClick(point.X, point.Y);
    }

    internal static object Read() => new {
        day = Game1.dayOfMonth,
        levels = Enumerable.Range(0, 5).Select(Game1.player.GetUnmodifiedSkillLevel).ToArray(),
        pendingLevels = Game1.player.newLevels.Count,
        professions = Game1.player.professions.Order().ToArray(),
        mastery = new {
            experience = Game1.stats.Get(StatKeys.MasteryExp), level = MasteryTrackerMenu.getCurrentMasteryLevel(),
            claimed = Enumerable.Range(0, 5).Select(skill => Game1.player.stats.Get(StatKeys.Mastery(skill))).ToArray(),
            spent = Game1.stats.Get(StatKeys.MasteryLevelsSpent), trinketSlots = Game1.player.stats.Get("trinketSlots"),
            complete = MasteryTrackerMenu.hasCompletedAllMasteryPlaques(),
            recipes = MasteryRecipes.ToDictionary(recipe => recipe, Game1.player.craftingRecipes.ContainsKey),
            items = Game1.player.Items.Where(item => item?.QualifiedItemId is "(W)66" or "(T)AdvancedIridiumRod")
                .Select(item => item.QualifiedItemId).Order().ToArray()
        },
        incomeAchievement = Game1.player.achievements.Contains(0),
        fish = Game1.player.fishCaught.GetValueOrDefault("(O)145")
    };

    internal static int[] ChooseProfessions()
    {
        var savedInput = Game1.input;
        bool savedGamepad = Game1.options.gamepadControls;
        var savedMenu = Game1.activeClickableMenu;
        var input = new ProfessionInput();
        try
        {
            Game1.input = input;
            Game1.options.gamepadControls = false;
            for (int skill = 0; skill < 5; skill++)
            {
                foreach (int level in new[] { 5, 10 })
                {
                    ShowProfession(skill, level);
                    var menu = (LevelUpMenu)Game1.activeClickableMenu;
                    var point = menu.leftProfession.bounds.Center;
                    input.Mouse = Mouse(point, ButtonState.Released);
                    for (int frame = 1; frame <= 32; frame++) menu.update(Time(frame));
                    input.Mouse = Mouse(point, ButtonState.Pressed);
                    menu.update(Time(33));
                    input.Mouse = Mouse(point, ButtonState.Released);
                    menu.update(Time(34));
                }
            }
            return Game1.player.professions.Order().ToArray();
        }
        finally
        {
            Game1.input = savedInput;
            Game1.options.gamepadControls = savedGamepad;
            Game1.activeClickableMenu = savedMenu;
        }
    }

    private static GameTime Time(int frame) => new(TimeSpan.FromMilliseconds(frame * 16), TimeSpan.FromMilliseconds(16));
    private static MouseState Mouse(Point point, ButtonState left) => new(point.X, point.Y, 0, left,
        ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

    private sealed class ProfessionInput : InputState
    {
        internal MouseState Mouse;
        public override KeyboardState GetKeyboardState() => default;
        public override MouseState GetMouseState() => Mouse;
        public override GamePadState GetGamePadState() => default;
    }
}
