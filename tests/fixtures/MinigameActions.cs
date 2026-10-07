using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Minigames;
using System.Reflection;

namespace StardewBrowser.Testing;

internal static class MinigameActions
{
    internal static void Prepare() => Game1.player.hasSkullKey = true;

    internal static object Run()
    {
        Random savedRandom = Game1.random;
        InputState savedInput = Game1.input;
        bool savedGamepad = Game1.options.gamepadControls;
        var input = new ReplayInput();
        try
        {
            Game1.random = new Random(1729);
            Game1.input = input;
            Game1.options.gamepadControls = false;
            var king = new AbigailGame();
            king.receiveKeyPress(Keys.Space);
            king.tick(Time(1));
            float startX = king.playerPosition.X;
            input.Keys = [Keys.D, Keys.Right];
            for (int step = 2; step <= 65; step++) king.tick(Time(step));
            king.SaveGame();
            var kingResult = new { started = !AbigailGame.onStartMenu, moved = king.playerPosition.X - startX,
                x = king.playerPosition.X, y = king.playerPosition.Y, bullets = king.bullets.Count,
                lives = king.lives, coins = king.coins, wave = AbigailGame.whichWave,
                savedWave = Game1.player.jotpkProgress.Value.whichWave.Value,
                savedLives = Game1.player.jotpkProgress.Value.lives.Value };
            king.unload();
            var karts = new List<object>();
            foreach (int mode in new[] { 2, 3 })
            {
                input.Keys = [];
                var cart = new MineCart(0, mode);
                for (int step = 1; step <= 80; step++) cart.tick(Time(step));
                for (int step = 81; step <= 680 && cart.gameState != MineCart.GameStates.Ingame; step++)
                {
                    input.Keys = step % 60 == 0 ? [Keys.Space] : [];
                    cart.tick(Time(step));
                }
                bool started = cart.gameState == MineCart.GameStates.Ingame;
                var player = Field<MineCart.MineCartCharacter>(cart, "player");
                float initialX = player.position.X, furthestX = initialX;
                for (int step = 681; step <= 800; step++)
                {
                    input.Keys = step % 45 < 12 ? [Keys.Space] : [];
                    cart.tick(Time(step));
                    furthestX = Math.Max(furthestX, player.position.X);
                }
                var tracks = Field<Dictionary<int, List<MineCart.Track>>>(cart, "_tracks");
                karts.Add(new { mode, started, startX = initialX, furthestX, x = player.position.X, y = player.position.Y,
                    state = cart.gameState.ToString(), theme = Field<int>(cart, "currentTheme"), tracks = tracks.Sum(pair => pair.Value.Count),
                    checkpoints = cart.checkpointPositions.Count, score = Field<int>(cart, "score"),
                    lives = Field<int>(cart, "livesLeft") });
                cart.unload();
            }
            return new { king = kingResult, kart = karts };
        }
        finally
        {
            Game1.input = savedInput;
            Game1.random = savedRandom;
            Game1.options.gamepadControls = savedGamepad;
        }
    }

    private static GameTime Time(int step) => new(TimeSpan.FromMilliseconds(step * 16), TimeSpan.FromMilliseconds(16));
    private static T Field<T>(MineCart cart, string name) =>
        (T)typeof(MineCart).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cart)!;

    // Controlled hardware input only: original minigames still interpret bindings and run every update.
    private sealed class ReplayInput : InputState
    {
        internal Keys[] Keys = [];
        public override KeyboardState GetKeyboardState() => new(Keys);
        public override MouseState GetMouseState() => default;
        public override GamePadState GetGamePadState() => default;
    }
}
