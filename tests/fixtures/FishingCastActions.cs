using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace StardewBrowser.Testing;

internal static class FishingCastActions
{
    internal const string FixtureKey = "StardewBrowser.FishingCastFixture";
    internal static FishingRod Rod => (FishingRod)Game1.player.Items[0];

    internal static void Prepare()
    {
        Game1.player.modData[FixtureKey] = "1";
        for (int i = 0; i < Game1.player.Items.Count; i++) Game1.player.Items[i] = null;
        Game1.player.Items[0] = ItemRegistry.Create<FishingRod>("(T)IridiumRod");
        Rod.attach(ItemRegistry.Create<StardewValley.Object>("(O)685", 2));
        Game1.player.CurrentToolIndex = 0;
        Game1.player.fishingLevel.Value = 5;
        Game1.player.experiencePoints[1] = 2150;
        Game1.player.professions.Add(6);
        Game1.player.fishCaught.Clear();
        Game1.stats.TimesFished = 0;
    }

    internal static Vector2 Shore()
    {
        var location = Game1.getLocationFromName("Town");
        for (int y = 4; y < location.Map.Layers[0].LayerHeight - 8; y++)
        for (int x = 4; x < location.Map.Layers[0].LayerWidth - 4; x++)
        {
            var tile = new Vector2(x, y);
            if (location.isWaterTile(x, y) || !location.isTilePassable(tile)
                || location.objects.ContainsKey(tile) || location.terrainFeatures.ContainsKey(tile)
                || location.doesTileHaveProperty(x, y, "Action", "Buildings") != null) continue;
            if (Enumerable.Range(4, 3).All(offset => location.isTileFishable(x, y + offset))) return tile;
        }
        throw new InvalidOperationException("The original Town needs a passable shore above fishable water.");
    }

    internal static object CastState() => new {
        timesFished = Game1.stats.TimesFished, stamina = Game1.player.Stamina,
        water = Game1.currentLocation.isTileFishable((int)(Rod.bobber.X / 64), (int)(Rod.bobber.Y / 64))
    };

    internal static object ReadState() => new {
        day = Game1.dayOfMonth, timesFished = Game1.stats.TimesFished,
        experience = Game1.player.experiencePoints[1], bait = Rod.GetBait()?.Stack ?? 0,
        inventory = Game1.player.Items.Where(item => item != null && item is not FishingRod)
            .Select(item => new { id = item.QualifiedItemId, stack = item.Stack, quality = item.Quality }).ToArray(),
        collection = Game1.player.fishCaught.Pairs.OrderBy(pair => pair.Key)
            .Select(pair => new { id = pair.Key, count = pair.Value[0], size = pair.Value[1] }).ToArray()
    };

    internal static object Read() => new {
        state = ReadState(), cast = CastState(),
        rod = new { power = Rod.castingPower, fishing = Rod.isFishing, nibbling = Rod.isNibbling, hit = Rod.hit, caught = Rod.fishCaught,
            keyDownSeen = Game1.oldKBState.IsKeyDown(Keys.C), nibbleElapsed = Rod.fishingNibbleAccumulator, nibbleLimit = Rod.timeUntilFishingNibbleDone },
        bar = Game1.activeClickableMenu is BobberBar bar ? new {
            fish = bar.bobberPosition, position = bar.bobberBarPos, speed = bar.bobberBarSpeed,
            height = bar.bobberBarHeight, distance = bar.distanceFromCatching, fadeOut = bar.fadeOut } : null
    };

    internal static object Run(Action frame)
    {
        var savedInput = Game1.input;
        bool savedGamepad = Game1.options.gamepadControls;
        var input = new CastInput();
        void Until(Func<bool> done, string stage)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!done())
            {
                if (timer.Elapsed > TimeSpan.FromSeconds(50)) throw new TimeoutException("Original fishing stalled at " + stage
                    + $"; power={Rod.castingPower}, fishing={Rod.isFishing}, nibbling={Rod.isNibbling}, hit={Rod.hit}, caught={Rod.fishCaught},"
                    + $" facing={Game1.player.FacingDirection}, using={Game1.player.UsingTool}, menu={Game1.activeClickableMenu?.GetType().Name}");
                frame();
            }
        }
        try
        {
            Game1.input = input;
            Game1.options.gamepadControls = false;
            Game1.player.faceDirection(2);
            Console.WriteLine($"Original rod shore {Game1.player.Tile}");
            input.Keys = [Keys.C];
            Until(() => Rod.castingPower >= 0.9f, "charging");
            input.Keys = [];
            Until(() => Rod.isFishing, "landing the cast");
            Console.WriteLine("Original rod cast landed in water");
            var cast = CastState();
            Until(() => Rod.isNibbling, "waiting for a bite");
            input.Keys = [Keys.C];
            frame();
            input.Keys = [];
            Until(() => Rod.hit, "hooking");
            Until(() => Game1.activeClickableMenu is BobberBar, "opening the fishing bar");
            Console.WriteLine("Original rod opened the fishing bar");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (Game1.activeClickableMenu is BobberBar bar)
            {
                if (timer.Elapsed > TimeSpan.FromSeconds(50)) throw new TimeoutException("Original fishing bar did not finish.");
                float target = bar.bobberPosition + 32 - bar.bobberBarHeight / 2f;
                float desiredSpeed = Math.Clamp((target - bar.bobberBarPos) * 0.12f, -3, 3);
                input.Keys = !bar.fadeOut && bar.bobberBarSpeed > desiredSpeed ? [Keys.C] : [];
                frame();
            }
            input.Keys = [];
            Until(() => Rod.fishCaught, "reeling in the catch");
            input.Keys = [Keys.C];
            Until(() => Game1.player.Items.Any(item => item != null && item is not FishingRod), "accepting the catch");
            input.Keys = [];
            Until(() => Game1.player.CanMove && !Game1.player.UsingTool, "finishing the rod");
            Console.WriteLine("Original rod catch accepted");
            return cast;
        }
        finally { Game1.input = savedInput; Game1.options.gamepadControls = savedGamepad; }
    }

    private sealed class CastInput : InputState
    {
        internal Keys[] Keys = [];
        public override KeyboardState GetKeyboardState() => new(Keys);
        public override MouseState GetMouseState() => new(
            (int)Game1.player.Position.X + 32 - Game1.viewport.X,
            (int)Game1.player.Position.Y + 160 - Game1.viewport.Y, 0,
            ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        public override GamePadState GetGamePadState() => default;
    }
}
