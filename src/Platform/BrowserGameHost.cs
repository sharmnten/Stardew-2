using Microsoft.JSInterop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using xTile;
using xTile.Display;
using xTile.Tiles;
using StardewBrowser.Platform.Compatibility;
using StardewBrowser.Platform.Content;
using System.Net.Http.Json;
using StardewBrowser.Platform.Audio;
using StardewValley;
using StardewBrowser.Framework.Graphics;
using StardewBrowser.Platform.Storage;
using StardewBrowser.GameStorage;

namespace StardewBrowser.Platform;

public sealed class BrowserGameHost(IJSRuntime js, HttpClient http, bool diagnostic = false) : IAsyncDisposable
{
    private Game? game;
    private GraphicsProbe? probe;
    private DotNetObjectReference<BrowserGameHost>? reference;
    private BrowserContentStore content = null!;
    private BrowserAudioAdapter audio = null!;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await js.InvokeVoidAsync("portHost.resize", cancellationToken);
            StardewBrowser.Framework.Graphics.OriginalContentReaders.Register();
            var manifest = await http.GetFromJsonAsync<StardewBrowser.Platform.Content.ContentManifest>("Content/manifest.json", cancellationToken)
                ?? throw new InvalidDataException("The original content manifest is empty.");
            content = new BrowserContentStore(http, manifest);
            audio = new BrowserAudioAdapter(js, http);
            await audio.InitializeAsync(cancellationToken);
            if (diagnostic) await audio.InitializeXactAsync(content, cancellationToken);
            else await audio.ConfigureXactAsync(content, cancellationToken);
            if (diagnostic)
            {
            foreach (string name in new[] { "TileSheets/crops", "Fonts/SmallFont", "Fonts/Japanese", "Maps/Farm", "Effects/ShadowRemoveMG3.8.0" })
                await FetchAsync(name, cancellationToken);
            game = probe = new GraphicsProbe(content);
            game.Run();
            foreach (var sheet in probe.Map.TileSheets)
                await FetchAsync(sheet.ImageSource, cancellationToken);
            probe.Prepare();
            }
            else
            {
                var saves = new BrowserSaveStore(new IndexedDbSaveBackend(js), validateImport: BrowserSaveBridge.ValidateImport);
                await saves.HydrateAsync();
                BrowserPersistence.Configure(saves);
                await js.InvokeVoidAsync("portHost.status", new { phase = "loading", message = "Verifying original game content…" });
                await content.PreloadAsync(manifest.Assets.Where(entry => entry.Group != "audio-bank").Select(entry => entry.Name), cancellationToken);
                OriginalContent.Configure(content);
                Directory.CreateDirectory(OriginalContent.Root + "/Content");
                Directory.SetCurrentDirectory(OriginalContent.Root);
                foreach (var entry in manifest.Assets.Where(entry => entry.Group != "audio-bank" && !entry.Path.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase)))
                {
                    string path = OriginalContent.Root + "/" + entry.Path;
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    using var stream = content.Open(entry.Name);
                    using var destination = File.Create(path);
                    stream.CopyTo(destination);
                }
                var size = ((IJSInProcessRuntime)js).Invoke<CanvasSize>("portHost.size");
                BrowserDisplay.Configure(() => {
                    var current = ((IJSInProcessRuntime)js).Invoke<CanvasSize>("portHost.size");
                    return new Point(current.Width, current.Height);
                });
                var runner = new GameRunner();
                GameRunner.instance = runner;
                Game1.graphics.GraphicsProfile = GraphicsProfile.HiDef;
                Game1.graphics.PreferredBackBufferWidth = size.Width;
                Game1.graphics.PreferredBackBufferHeight = size.Height;
                game = runner;
                game.Run();
            }
            reference = DotNetObjectReference.Create(this);
            await js.InvokeVoidAsync("portHost.start", cancellationToken, reference);
        }
        catch (Exception error) { await js.InvokeVoidAsync("portHost.status", new { phase = "failed", error = error.ToString() }); }
    }

    private Task FetchAsync(string name, CancellationToken cancellationToken) => content.PreloadAsync([name], cancellationToken);
    public Task PlayAudioProbeAsync() => audio.PlayProbeAsync();

    public Task<string[]> ListSavesAsync() => BrowserPersistence.Current.Store.ListSlotsAsync();
    public void RetrySave() => BrowserPersistence.Current.Retry();

    public async Task ImportSaveAsync(IReadOnlyDictionary<string, byte[]> files)
    {
        if (Game1.activeClickableMenu is not StardewValley.Menus.TitleMenu || StardewValley.Menus.TitleMenu.subMenu != null)
            throw new InvalidOperationException("Return to the title screen before importing a save.");
        await BrowserPersistence.Current.Store.ImportAsync(files);
    }

    public async Task ExportSaveAsync(string slot)
    {
        var files = await BrowserPersistence.Current.Store.ExportAsync(slot);
        await js.InvokeVoidAsync("portStorage.download", slot + ".zip", SaveArchive.Create(slot, files));
    }

    public async Task ExportPendingAsync()
    {
        var pending = BrowserPersistence.Current.Pending;
        if (pending == null)
        {
            await ExportSaveAsync(BrowserPersistence.Current.Status.Slot ?? throw new InvalidOperationException("No recoverable save exists."));
            return;
        }
        if (pending.Slot == BrowserSaveStore.SettingsSlot)
            await js.InvokeVoidAsync("portStorage.download", "startup_preferences", pending.Files["startup_preferences"]);
        else await js.InvokeVoidAsync("portStorage.download", pending.Slot + "-pending.zip", SaveArchive.Create(pending.Slot, pending.Files));
    }

    [JSInvokable]
    public void Tick()
    {
        try
        {
            game!.Tick();
            audio.UpdateFrame();
            object status = probe != null ? probe.Status(content.ResidentBytes)
                : new { phase = "ready", verifiedContentBytes = content.ResidentBytes, game = GameSnapshot.Read(), storage = BrowserPersistence.Current.Status };
            ((IJSInProcessRuntime)js).InvokeVoid("portHost.status", status);
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
        if (audio != null) await audio.DisposeAsync();
        reference?.Dispose();
    }
}

internal sealed record CanvasSize(int Width, int Height);

internal sealed class ResidentContent(IServiceProvider services, BrowserContentStore store) : ContentManager(services)
{
    protected override Stream OpenStream(string assetName)
    {
        var source = store.Open(assetName);
        if (assetName.Replace('\\', '/') != "Effects/ShadowRemoveMG3.8.0") return source;
        using (source)
        using (var buffer = new MemoryStream())
        {
            source.CopyTo(buffer);
            return new MemoryStream(LegacyEffect.ConvertXnb(buffer.ToArray()), writable: false);
        }
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
    private int customFontCharacters;

    public GraphicsProbe(BrowserContentStore content)
    {
        graphics = new GraphicsDeviceManager(this) { GraphicsProfile = GraphicsProfile.HiDef, PreferredBackBufferWidth = 960, PreferredBackBufferHeight = 596 };
        Content = new ResidentContent(Services, content);
        IsMouseVisible = true;
        IsFixedTimeStep = false;
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        crops = Content.Load<Texture2D>("TileSheets/crops");
        font = Content.Load<SpriteFont>("Fonts/SmallFont");
        customFontCharacters = BmFont.FontLoader.Parse(Content.Load<BmFont.XmlSource>("Fonts/Japanese").Source).Chars.Count;
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
        catch (Exception error) { effectError = error.ToString(); }
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

    public object Status(long verifiedContentBytes) => new { phase = "ready", error = (string?)null, textureWidth = crops.Width,
        fontGlyphs = font.Characters.Count, mapLayers = Map.Layers.Count, renderedTiles = display.TilesDrawn,
        targetDistinctColors = distinctColors, fontPixels, effectVerified, effectError, cursorX, pointerClicks = clicks,
        customFontCharacters, verifiedContentBytes, renderer = typeof(SpriteBatch).FullName,
        originalTextureReads = StardewBrowser.Framework.Graphics.OriginalContentReaders.TextureReads,
        desktopFrameworkLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "MonoGame.Framework") };
}
