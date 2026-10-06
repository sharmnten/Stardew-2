using Microsoft.JSInterop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using xTile;
using xTile.Display;
using xTile.Tiles;
using StardewBrowser.Platform.Compatibility;

namespace StardewBrowser.Platform;

public sealed class BrowserGameHost(IJSRuntime js, HttpClient http) : IAsyncDisposable
{
    private GraphicsProbe? game;
    private DotNetObjectReference<BrowserGameHost>? reference;
    private readonly Dictionary<string, byte[]> assets = new(StringComparer.OrdinalIgnoreCase);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await js.InvokeVoidAsync("portHost.resize", cancellationToken);
            foreach (string name in new[] { "TileSheets/crops", "Fonts/SmallFont", "Maps/Farm", "Effects/ShadowRemoveMG3.8.0" })
                await FetchAsync(name, cancellationToken);
            game = new GraphicsProbe(assets);
            game.Run();
            foreach (var sheet in game.Map.TileSheets)
                await FetchAsync(sheet.ImageSource, cancellationToken);
            game.Prepare();
            reference = DotNetObjectReference.Create(this);
            await js.InvokeVoidAsync("portHost.start", cancellationToken, reference);
        }
        catch (Exception error) { await js.InvokeVoidAsync("portHost.status", new { phase = "failed", error = error.ToString() }); }
    }

    private async Task FetchAsync(string name, CancellationToken cancellationToken)
    {
        name = name.Replace('\\', '/');
        if (assets.ContainsKey(name)) return;
        assets[name] = await http.GetByteArrayAsync("Content/" + name + ".xnb", cancellationToken);
    }

    [JSInvokable]
    public void Tick()
    {
        try
        {
            game!.Tick();
            ((IJSInProcessRuntime)js).InvokeVoid("portHost.status", game.Status());
        }
        catch (Exception error)
        {
            ((IJSInProcessRuntime)js).InvokeVoid("portHost.status", new { phase = "failed", error = error.ToString() });
        }
    }

    public async ValueTask DisposeAsync()
    {
        await js.InvokeVoidAsync("portHost.stop");
        game?.Dispose();
        reference?.Dispose();
    }
}

internal sealed class ResidentContent(IServiceProvider services, Dictionary<string, byte[]> assets) : ContentManager(services)
{
    protected override Stream OpenStream(string assetName)
    {
        string key = assetName.Replace('\\', '/');
        if (!assets.TryGetValue(key, out var data)) throw new ContentLoadException("Asset not preloaded: " + key);
        if (key == "Effects/ShadowRemoveMG3.8.0") data = LegacyEffect.ConvertXnb(data);
        return new MemoryStream(data, writable: false);
    }
}

internal sealed class TileDisplay(ContentManager content, GraphicsDevice device) : XnaDisplayDevice(content, device)
{
    public int TilesDrawn { get; private set; }
    public override void DrawTile(Tile tile, xTile.Dimensions.Location location, float layerDepth)
    {
        base.DrawTile(tile, location, layerDepth);
        if (tile != null) TilesDrawn++;
    }
}

internal sealed class GraphicsProbe : Game
{
    private readonly GraphicsDeviceManager graphics;
    private SpriteBatch batch = null!;
    private Texture2D crops = null!;
    private SpriteFont font = null!;
    private RenderTarget2D scene = null!, text = null!;
    private TileDisplay display = null!;
    public Map Map { get; private set; } = null!;
    private int distinctColors, fontPixels, clicks;
    private float cursorX = 48;
    private bool previousPressed;
    private string? effectError;
    private bool effectVerified;

    public GraphicsProbe(Dictionary<string, byte[]> assets)
    {
        graphics = new GraphicsDeviceManager(this) { GraphicsProfile = GraphicsProfile.HiDef, PreferredBackBufferWidth = 960, PreferredBackBufferHeight = 596 };
        Content = new ResidentContent(Services, assets);
        IsMouseVisible = true;
        IsFixedTimeStep = false;
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        crops = Content.Load<Texture2D>("TileSheets/crops");
        font = Content.Load<SpriteFont>("Fonts/SmallFont");
        Map = Content.Load<Map>("Maps/Farm");
    }

    public void Prepare()
    {
        display = new TileDisplay(Content, GraphicsDevice);
        Map.LoadTileSheets(display);
        scene = new RenderTarget2D(GraphicsDevice, 640, 384);
        text = new RenderTarget2D(GraphicsDevice, 640, 64);
        GraphicsDevice.SetRenderTarget(scene);
        GraphicsDevice.Clear(Color.Transparent);
        batch.Begin(samplerState: SamplerState.PointClamp);
        display.BeginScene(batch);
        foreach (var layer in Map.Layers)
            layer.Draw(display, new xTile.Dimensions.Rectangle(0, 0, 640, 384), xTile.Dimensions.Location.Origin, false, 2);
        batch.Draw(crops, new Vector2(16, 16), new Rectangle(0, 0, 128, 128), Color.White);
        batch.End();
        GraphicsDevice.SetRenderTarget(text);
        GraphicsDevice.Clear(Color.Transparent);
        batch.Begin();
        batch.DrawString(font, "Stardew Valley 1.6.15", new Vector2(12, 12), Color.White);
        batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[640 * 384];
        scene.GetData(pixels);
        distinctColors = pixels.Distinct().Count();
        var glyphPixels = new Color[640 * 64];
        text.GetData(glyphPixels);
        fontPixels = glyphPixels.Count(pixel => pixel.A > 0);
        try
        {
            var effect = Content.Load<Effect>("Effects/ShadowRemoveMG3.8.0");
            using var sample = new Texture2D(GraphicsDevice, 2, 1);
            sample.SetData(new[] { new Color(100, 0, 0, 100), Color.Lime });
            using var output = new RenderTarget2D(GraphicsDevice, 2, 1);
            GraphicsDevice.SetRenderTarget(output);
            GraphicsDevice.Clear(Color.Transparent);
            batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp, effect: effect);
            batch.Draw(sample, new Rectangle(0, 0, 2, 1), Color.White);
            batch.End();
            GraphicsDevice.SetRenderTarget(null);
            var filtered = new Color[2];
            output.GetData(filtered);
            effectVerified = filtered[0].A == 0 && filtered[1].G == 255 && filtered[1].A == 255;
            if (!effectVerified) effectError = "Original shadow filter changed the expected pixel alpha or color.";
        }
        catch (Exception error) { effectError = error.Message; }
    }

    protected override void Update(GameTime time)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Right)) cursorX += 180 * (float)time.ElapsedGameTime.TotalSeconds;
        if (Keyboard.GetState().IsKeyDown(Keys.Left)) cursorX -= 180 * (float)time.ElapsedGameTime.TotalSeconds;
        bool pressed = Mouse.GetState().LeftButton == ButtonState.Pressed;
        if (pressed && !previousPressed) clicks++;
        previousPressed = pressed;
        base.Update(time);
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(new Color(23, 44, 43));
        batch.Begin(samplerState: SamplerState.PointClamp);
        batch.Draw(scene, new Vector2(24, 88), Color.White);
        batch.Draw(text, Vector2.Zero, Color.White);
        batch.Draw(crops, new Vector2(cursorX, 500), new Rectangle(0, 0, 16, 32), Color.White, 0, Vector2.Zero, 2, SpriteEffects.None, 0);
        batch.End();
        base.Draw(time);
    }

    public object Status() => new { phase = "ready", error = (string?)null, textureWidth = crops.Width,
        fontGlyphs = font.Characters.Count, mapLayers = Map.Layers.Count, renderedTiles = display.TilesDrawn,
        targetDistinctColors = distinctColors, fontPixels, effectVerified, effectError, cursorX, pointerClicks = clicks };
}
