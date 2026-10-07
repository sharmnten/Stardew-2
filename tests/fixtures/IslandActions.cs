using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.SpecialOrders;

namespace StardewBrowser.Testing;

internal static class IslandActions
{
    internal static void Prepare() => Game1.player.mailReceived.Add("willyBoatFixed");

    internal static object Run()
    {
        Random savedRandom = Game1.random;
        try
        {
            Game1.random = new Random(1729);
            float before = Utility.percentGameComplete();
            for (int attempt = 0; attempt < 7; attempt++)
            {
                Game1.player.team.RequestLimitedNutDrops("IslandFishing", null, 0, 0, 5);
                Game1.player.team.Update();
            }
            var hut = Game1.RequireLocation<IslandHut>("IslandHut");
            var perch = hut.parrotUpgradePerches.Single();
            int balance = Game1.netWorldState.Value.GoldenWalnuts;
            perch.AttemptConstruction();
            for (int step = 1; step <= 12; step++)
                perch.UpdateEvenIfFarmerIsntHere(new GameTime(TimeSpan.FromMilliseconds(step * 400), TimeSpan.FromMilliseconds(400)));
            Game1.player.team.Update();
            var parrot = new { cost = balance - Game1.netWorldState.Value.GoldenWalnuts,
                complete = hut.firstParrotDone.Value, state = perch.currentState.Value.ToString(),
                mail = Game1.player.mailForTomorrow.Where(mail => mail.StartsWith("Island_")).Order().ToArray() };
            var walnuts = new { limitedDrops = Game1.player.team.GetDroppedLimitedNutCount("IslandFishing"),
                found = Game1.netWorldState.Value.GoldenWalnutsFound, balance = Game1.netWorldState.Value.GoldenWalnuts };
            var qi = DataLoader.SpecialOrders(Game1.content).Where(pair => pair.Value.OrderType == "Qi").OrderBy(pair => pair.Key)
                .Select(pair => {
                    var order = SpecialOrder.GetSpecialOrder(pair.Key, 1729)
                        ?? throw new InvalidOperationException("Original Qi order construction failed: " + pair.Key);
                    return new { id = order.questKey.Value, type = order.orderType.Value, name = order.GetName(),
                        condition = order.GetData().Condition, tags = order.GetData().RequiredTags,
                        tagsMatch = SpecialOrder.CheckTags(order.GetData().RequiredTags),
                        definedObjectives = order.GetData().Objectives.Select(objective => objective.Type).ToArray(),
                        objectives = order.objectives.Select(objective => new { type = objective.GetType().Name,
                            count = objective.GetCount(), required = objective.GetMaxCount(), description = objective.GetDescription() }).ToArray(),
                        rewards = order.rewards.Select(reward => reward.GetType().Name).ToArray() };
                }).ToArray();
            return new { walnuts, parrot, qi, perfection = new { before, after = Utility.percentGameComplete() } };
        }
        finally { Game1.random = savedRandom; }
    }
}
