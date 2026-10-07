using StardewValley;
using StardewValley.Constants;
using StardewValley.Menus;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace StardewBrowser.Testing;

internal static class ProgressionActions
{
    internal static void Prepare()
    {
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

    internal static void ShowProfession(int level) => Game1.activeClickableMenu = new LevelUpMenu(0, level);

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
            foreach (int level in new[] { 5, 10 })
            {
                ShowProfession(level);
                var menu = (LevelUpMenu)Game1.activeClickableMenu;
                var point = menu.leftProfession.bounds.Center;
                input.Mouse = Mouse(point, ButtonState.Released);
                for (int frame = 1; frame <= 32; frame++) menu.update(Time(frame));
                input.Mouse = Mouse(point, ButtonState.Pressed);
                menu.update(Time(33));
                input.Mouse = Mouse(point, ButtonState.Released);
                menu.update(Time(34));
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
