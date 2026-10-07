using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewBrowser.Testing;

internal static class StoryActions
{
    internal static void Prepare(string id)
    {
        uint earnings = Game1.player.totalMoneyEarned;
        Game1.player.Money = 50000;
        Game1.player.totalMoneyEarned = earnings;
        for (int i = 0; i < Game1.player.Items.Count; i++) Game1.player.Items[i] = null;
        if (id == "community-center")
        {
            Game1.player.mailReceived.Add("canReadJunimoText");
            string[] crops = ["24", "188", "190", "192"];
            for (int i = 0; i < crops.Length; i++) Game1.player.Items[i] = ItemRegistry.Create("(O)" + crops[i]);
        }
        else Game1.player.mailReceived.Add("JojaMember");
    }

    internal static object Run(string id)
    {
        if (id == "community-center")
        {
            var center = Game1.RequireLocation<CommunityCenter>("CommunityCenter");
            var menu = new JunimoNoteMenu(0, center.bundlesDict());
            Game1.activeClickableMenu = menu;
            var bundle = menu.bundles.Single(bundle => bundle.bundleIndex == 0);
            menu.receiveLeftClick(bundle.bounds.Center.X, bundle.bounds.Center.Y);
            for (int i = 0; i < 4; i++)
            {
                var item = menu.inventory.inventory[i].bounds.Center;
                menu.receiveLeftClick(item.X, item.Y);
                var slot = menu.ingredientSlots[i].bounds.Center;
                menu.receiveLeftClick(slot.X, slot.Y);
            }
            for (int step = 1; step <= 4; step++)
                menu.update(new GameTime(TimeSpan.FromMilliseconds(step * 500), TimeSpan.FromMilliseconds(500)));
            var reward = bundle.getReward();
            var result = new { completed = center.isBundleComplete(0),
                consumed = 4 - Game1.player.Items.Take(4).Count(item => item != null),
                rewardAvailable = center.bundleRewards[0], rewardId = reward.QualifiedItemId, rewardStack = reward.Stack };
            menu.exitThisMenu();
            return new { bundle = result };
        }
        else
        {
            var menu = new JojaCDMenu(Game1.temporaryContent.Load<Texture2D>("LooseSprites\\JojaCDForm"));
            Game1.activeClickableMenu = menu;
            int money = Game1.player.Money;
            var button = menu.checkboxes[0].bounds.Center;
            menu.receiveLeftClick(button.X, button.Y);
            return new { project = new { cost = money - Game1.player.Money,
                completedCheckbox = menu.checkboxes[0].name == "complete",
                mail = Game1.player.mailForTomorrow.Order().ToArray() } };
        }
    }
}
