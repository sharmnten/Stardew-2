using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.InteropServices;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.SDKs;
using StardewValley.Menus;
using NativeProgram = StardewValley.Program;

if (args.Length is < 2 or > 3) throw new ArgumentException("Usage: DesktopReference <original-root> <report.json> [scenario-id]");
string root = Path.GetFullPath(args[0]);
string report = Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.Resolving += (_, name) => {
    string path = Path.Combine(root, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
// Preserve the Windows assembly and content bytes. Linux treats backslashes as
// filename characters, so expose aliases alongside normal relative paths.
string contentRoot = Path.Combine(Path.GetDirectoryName(report)!, "Content");
Directory.CreateDirectory(contentRoot);
foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "Content"), "*", SearchOption.AllDirectories))
{
    string relative = Path.GetRelativePath(Path.Combine(root, "Content"), file);
    string[] parts = relative.Split(Path.DirectorySeparatorChar);
    for (int split = 0; split < parts.Length; split++)
    {
        string prefix = split == 0 ? "" : Path.Combine(parts[..split]);
        string suffix = string.Join('\\', parts[split..]);
        string alias = Path.Combine(contentRoot, prefix, suffix);
        Directory.CreateDirectory(Path.GetDirectoryName(alias)!);
        if (!File.Exists(alias)) File.CreateSymbolicLink(alias, file);
    }
}
Directory.SetCurrentDirectory(Path.GetDirectoryName(report)!);
typeof(NativeProgram).GetField("_sdk", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, new NullSDKHelper());
using var runner = new GameRunner();
GameRunner.instance = runner;
runner.Content.RootDirectory = contentRoot;
Console.WriteLine("Initialize original desktop frame");
NativeWindow.Frame(runner);
Console.WriteLine("Original desktop title initialized");
NativeWindow.Show(runner.Window.Handle);
var farmLayouts = Enumerable.Range(0, 7)
    .Select(index => new { id = index.ToString(), map = Farm.getMapNameFromTypeInt(index) }).ToList();
farmLayouts.AddRange(DataLoader.AdditionalFarms(Game1.content).Select(farm => new { id = farm.Id, map = farm.MapName }));
object? scenario = args.Length == 3
    ? args[2] == "browser-export" ? LoadBrowserExport(runner, report) : CreateFarm(args[2], runner, report)
    : null;
var result = new {
    gameVersion = typeof(Game1).Assembly.GetName().Version!.ToString(),
    displayVersion = Game1.version,
    gameAssembly = typeof(Game1).Assembly.FullName,
    frameworkAssembly = typeof(Game).Assembly.FullName,
    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    menu = Game1.activeClickableMenu?.GetType().Name,
    farmLayouts,
    scenario,
    graphics = new { maxTextureSize = GameRunner.MaxTextureSize, profile = runner.GraphicsDevice.GraphicsProfile.ToString() }
};
Directory.CreateDirectory(Path.GetDirectoryName(report)!);
File.WriteAllText(report, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) + "\n");
if (args.Length == 3)
{
    string fixtures = Path.Combine(Path.GetDirectoryName(report)!, "fixtures");
    Directory.CreateDirectory(fixtures);
    File.Copy(report, Path.Combine(fixtures, args[2] + ".json"), true);
}
Console.WriteLine($"Supplied desktop game initialized {result.menu}; {farmLayouts.Count} original farm layouts.");

static object LoadBrowserExport(GameRunner runner, string report)
{
    string archive = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(report)!, "..", "task-7-advanced-export.zip"));
    using var zip = ZipFile.OpenRead(archive);
    string slot = zip.Entries.Select(entry => entry.FullName.Split('/')[0]).Distinct().Single();
    if (slot != Path.GetFileName(slot) || slot is "" or "." or "..") throw new InvalidDataException("Invalid exported slot.");
    string destination = Path.Combine(NativeProgram.GetSavesFolder(), slot);
    Directory.CreateDirectory(destination);
    foreach (var entry in zip.Entries)
    {
        string[] parts = entry.FullName.Split('/');
        if (parts.Length != 2 || parts[0] != slot || entry.Length > 128 * 1024 * 1024
            || !new[] { slot, "SaveGameInfo", slot + "_old", "SaveGameInfo_old" }.Contains(parts[1]))
            throw new InvalidDataException("Unexpected browser-export entry.");
        entry.ExtractToFile(Path.Combine(destination, parts[1]), overwrite: true);
    }
    var menu = new LoadGameMenu();
    ((TitleMenu)Game1.activeClickableMenu).ForceSubmenu(menu);
    LoadGameMenu.SaveFileSlot? selected = null;
    Until(() => (selected = menu.MenuSlots.OfType<LoadGameMenu.SaveFileSlot>()
        .FirstOrDefault(candidate => candidate.Farmer.slotName == slot)) != null);
    selected!.Activate();
    Until(() => Game1.gameMode == 3 && !SaveGame.IsProcessing && Game1.player.CanMove
        && !Game1.isWarping && Game1.activeClickableMenu == null);
    return new { id = "browser-export", advanced = StardewBrowser.Testing.AdvancedActions.Read() };

    void Until(Func<bool> done)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!done())
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Original desktop browser-export load did not complete.");
            NativeWindow.Frame(runner); Thread.Sleep(1);
        }
    }
}

static object CreateFarm(string id, GameRunner runner, string report)
{
    var choices = new Dictionary<string, string> {
        ["new-game-standard"] = "Standard", ["farming-season"] = "Standard",
        ["inventory-economy"] = "Standard", ["recipes-machines"] = "Standard",
        ["advanced-desktop-roundtrip"] = "Standard", ["new-game-riverland"] = "Riverland",
        ["new-game-forest"] = "Forest", ["new-game-hilltop"] = "Hills",
        ["new-game-wilderness"] = "Wilderness", ["new-game-four-corners"] = "Four Corners",
        ["new-game-beach"] = "Beach", ["new-game-meadowlands"] = "ModFarm_MeadowlandsFarm"
    };
    string choice = choices[id];
    Console.WriteLine("Create original farmer " + id);
    var title = (TitleMenu)Game1.activeClickableMenu;
    var menu = new CharacterCustomization(CharacterCustomization.Source.NewGame);
    title.ForceSubmenu(menu);
    void Text(string field, string value) => ((TextBox)typeof(CharacterCustomization)
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(menu)!).Text = value;
    Text("nameBox", "Reference"); Text("farmnameBox", "Parity"); Text("favThingBox", "Trees");
    for (int page = 0; page < 3; page++)
    {
        var button = menu.farmTypeButtons.FirstOrDefault(button => button.name == choice);
        if (button != null) { menu.receiveLeftClick(button.bounds.Center.X, button.bounds.Center.Y); break; }
        var next = menu.farmTypeNextPageButton ?? throw new InvalidOperationException("Missing farm selector " + choice);
        menu.receiveLeftClick(next.bounds.Center.X, next.bounds.Center.Y);
    }
    menu.receiveLeftClick(menu.skipIntroButton.bounds.Center.X, menu.skipIntroButton.bounds.Center.Y);
    Console.WriteLine("Draw original customization");
    NativeWindow.Frame(runner); // Original draw commits textbox values to the farmer.
    Game1.startingGameSeed = 123456789;
    Console.WriteLine("Click original OK");
    menu.receiveLeftClick(menu.okButton.bounds.Center.X, menu.okButton.bounds.Center.Y);
    Console.WriteLine("Original OK returned");
    var taskField = typeof(Game1).GetField("_newDayTask", BindingFlags.Static | BindingFlags.NonPublic)!;
    var timer = System.Diagnostics.Stopwatch.StartNew();
    int frames = 0;
    while (Game1.dayOfMonth != 1 || taskField.GetValue(null) != null || Game1.game1.IsSaving
        || Game1.showingEndOfNightStuff || !Game1.player.CanMove)
    {
        if (timer.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException($"Original new day did not complete: day={Game1.dayOfMonth}, task={taskField.GetValue(null)}, saving={Game1.game1.IsSaving}, canMove={Game1.player.CanMove}, active={runner.IsActive}, mode={Game1.gameMode}, menu={Game1.activeClickableMenu?.GetType().Name}");
        if (frames++ % 120 == 0) Console.WriteLine($"Original frame {frames}: day={Game1.dayOfMonth}, task={taskField.GetValue(null)}, saving={Game1.game1.IsSaving}, canMove={Game1.player.CanMove}, active={runner.IsActive}, mode={Game1.gameMode}, menu={Game1.activeClickableMenu?.GetType().Name}");
        NativeWindow.Frame(runner);
        Thread.Sleep(1);
    }
    if (id is "farming-season" or "inventory-economy" or "recipes-machines" or "advanced-desktop-roundtrip")
    {
        Until(() => Game1.activeClickableMenu == null, "finish initial save menu");
        switch (id)
        {
            case "farming-season": StardewBrowser.Testing.FarmingActions.Prepare(); break;
            case "inventory-economy": StardewBrowser.Testing.EconomyActions.Prepare(); break;
            case "recipes-machines": StardewBrowser.Testing.ProductionActions.Prepare(); break;
            case "advanced-desktop-roundtrip": StardewBrowser.Testing.AdvancedActions.Prepare(); break;
        }
        Game1.game1.IsSaving = true;
        try
        {
            var saving = SaveGame.Save();
            var saveTime = System.Diagnostics.Stopwatch.StartNew();
            while (saving.MoveNext() && saving.Current < 100)
            {
                if (saveTime.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Original fixture save did not finish.");
                NativeWindow.Frame(runner); Thread.Sleep(1);
            }
        }
        finally { Game1.game1.IsSaving = false; }
    }
    string slot = SaveGame.FilterFileName(Game1.GetSaveGameName()) + "_" + Game1.uniqueIDForThisGame;
    string saved = Path.Combine(NativeProgram.GetSavesFolder(), slot);
    string fixture = Path.Combine(Path.GetDirectoryName(report)!, "fixtures", id, slot);
    Directory.CreateDirectory(fixture);
    foreach (string name in new[] { slot, "SaveGameInfo" }) File.Copy(Path.Combine(saved, name), Path.Combine(fixture, name), true);
    var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(ReadFarm()))!;
    state["id"] = JsonSerializer.SerializeToElement(id);
    state["saveFiles"] = JsonSerializer.SerializeToElement(Directory.GetFiles(fixture)
        .Select(file => new { name = Path.GetFileName(file), bytes = new FileInfo(file).Length }).ToArray());
    Reload();
    state["afterLoad"] = JsonSerializer.SerializeToElement(ReadFarm());
    if (id == "farming-season") state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.FarmingActions.Run());
    if (id == "inventory-economy") state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.EconomyActions.Run());
    if (id == "recipes-machines") state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.ProductionActions.Run());
    if (id == "advanced-desktop-roundtrip")
    {
        state["advancedBefore"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.AdvancedActions.Read());
        StardewBrowser.Testing.AdvancedActions.BeginSleep();
        bool nightMenuSeen = false;
        Until(() => {
            nightMenuSeen |= Game1.activeClickableMenu is ShippingMenu or SaveGameMenu;
            return nightMenuSeen && Game1.season == Season.Summer && Game1.dayOfMonth == 1
                && taskField.GetValue(null) == null && !Game1.showingEndOfNightStuff && !Game1.game1.IsSaving
                && Game1.player.CanMove && Game1.activeClickableMenu == null;
        }, "complete advanced overnight");
        state["afterSleep"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.AdvancedActions.Read());
        Reload();
        state["afterSleepReload"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.AdvancedActions.Read());
    }
    return state;

    void Reload()
    {
        bool returned = false;
        Game1.ExitToTitle(() => returned = true);
        Until(() => returned, "return to title");
        var loading = new LoadGameMenu();
        ((TitleMenu)Game1.activeClickableMenu).ForceSubmenu(loading);
        LoadGameMenu.SaveFileSlot? selected = null;
        Until(() => (selected = loading.MenuSlots.OfType<LoadGameMenu.SaveFileSlot>()
            .FirstOrDefault(candidate => candidate.Farmer.slotName == slot)) != null, "list original save");
        selected!.Activate();
        Until(() => Game1.gameMode == 3 && !SaveGame.IsProcessing && Game1.player.CanMove
            && !Game1.isWarping && Game1.activeClickableMenu == null, "reload original save");
    }

    void Until(Func<bool> done, string action)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!done())
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Reference could not " + action);
            NativeWindow.Frame(runner);
            if (Game1.activeClickableMenu is ShippingMenu shipping && shipping.CanReceiveInput())
                shipping.receiveLeftClick(shipping.okButton.bounds.Center.X, shipping.okButton.bounds.Center.Y);
            Thread.Sleep(1);
        }
    }
}

static object ReadFarm()
{
    var farm = Game1.getFarm();
    return new {
        day = Game1.dayOfMonth, farmId = Game1.GetFarmTypeID(), map = farm.mapPath.Value,
        farmer = new { name = Game1.player.Name, customized = Game1.player.isCustomized.Value, money = Game1.player.Money },
        buildings = farm.buildings.Select(building => new { type = building.buildingType.Value,
            x = building.tileX.Value, y = building.tileY.Value }).OrderBy(building => building.type).ToArray(),
        animals = farm.getAllFarmAnimals().Select(animal => new { type = animal.type.Value, name = animal.Name }).OrderBy(animal => animal.type).ToArray(),
        locationCount = Game1.locations.Count,
        locations = Game1.locations.Select(location => location.NameOrUniqueName).Order().ToArray()
    };

}

internal static class NativeWindow
{
    private static readonly MethodInfo Drain = typeof(Game).Assembly.GetType("Microsoft.Xna.Framework.Threading")!
        .GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo DisposeContexts = typeof(Microsoft.Xna.Framework.Graphics.GraphicsDevice)
        .GetMethod("DisposeContexts", BindingFlags.Static | BindingFlags.NonPublic)!;
    internal static void Frame(GameRunner runner)
    {
        runner.RunOneFrame();
        // Match the original SDL RunLoop: queued GPU work must run on the UI thread.
        Drain.Invoke(null, null);
        DisposeContexts.Invoke(null, null);
    }

    // RunOneFrame does not enter MonoGame's RunLoop, which normally shows this window.
    [DllImport("libSDL2-2.0.so.0", EntryPoint = "SDL_ShowWindow")]
    internal static extern void Show(IntPtr window);
}
