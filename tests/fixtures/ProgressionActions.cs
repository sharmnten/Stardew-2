using StardewValley;
using StardewValley.Constants;
using StardewValley.Menus;

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
}
