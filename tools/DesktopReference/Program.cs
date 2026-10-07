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
        ["inventory-economy"] = "Standard", ["tool-upgrades"] = "Standard", ["recipes-machines"] = "Standard", ["museum-quests"] = "Standard",
        ["island-qi-perfection"] = "Standard", ["original-minigames"] = "Standard", ["festivals-events-movies"] = "Standard", ["community-center"] = "Standard", ["joja-orders-museum"] = "Standard", ["skills-mastery-achievements"] = "Standard", ["characters-family"] = "Standard", ["combat-dungeons"] = "Standard", ["fishing-gathering"] = "Standard", ["fishing-cast"] = "Standard", ["animals-buildings"] = "Standard", ["text-sign-clipboard"] = "Standard", ["tailoring-automation-decoration"] = "Standard", ["advanced-desktop-roundtrip"] = "Standard", ["new-game-riverland"] = "Riverland",
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
    if (id is "farming-season" or "inventory-economy" or "tool-upgrades" or "recipes-machines" or "advanced-desktop-roundtrip" or "tailoring-automation-decoration" or "text-sign-clipboard" or "animals-buildings" or "fishing-gathering" or "fishing-cast" or "combat-dungeons" or "characters-family" or "skills-mastery-achievements" or "community-center" or "joja-orders-museum" or "museum-quests" or "festivals-events-movies" or "original-minigames" or "island-qi-perfection")
    {
        Until(() => Game1.activeClickableMenu == null, "finish initial save menu");
        if (id == "animals-buildings") GoToFarm();
        switch (id)
        {
            case "farming-season": StardewBrowser.Testing.FarmingActions.Prepare(); break;
            case "inventory-economy": StardewBrowser.Testing.EconomyActions.Prepare(); break;
            case "tool-upgrades": StardewBrowser.Testing.ToolUpgradeActions.Prepare(); break;
            case "museum-quests": StardewBrowser.Testing.MuseumActions.Prepare(); break;
            case "recipes-machines": StardewBrowser.Testing.ProductionActions.Prepare(); break;
            case "tailoring-automation-decoration": StardewBrowser.Testing.DecorationActions.Prepare(); break;
            case "text-sign-clipboard": StardewBrowser.Testing.TextSignActions.Prepare(); break;
            case "animals-buildings": StardewBrowser.Testing.AnimalActions.Prepare(); break;
            case "fishing-gathering": StardewBrowser.Testing.FishingActions.Prepare(); break;
            case "fishing-cast": StardewBrowser.Testing.FishingCastActions.Prepare(); break;
            case "island-qi-perfection": StardewBrowser.Testing.IslandActions.Prepare(); break;
            case "original-minigames": StardewBrowser.Testing.MinigameActions.Prepare(); break;
            case "festivals-events-movies": StardewBrowser.Testing.CalendarActions.Prepare(); break;
            case "community-center": case "joja-orders-museum": StardewBrowser.Testing.StoryActions.Prepare(id); break;
            case "skills-mastery-achievements": StardewBrowser.Testing.ProgressionActions.Prepare(); break;
            case "characters-family": StardewBrowser.Testing.FamilyActions.Prepare(); break;
            case "combat-dungeons": StardewBrowser.Testing.CombatActions.Prepare(); break;
            case "advanced-desktop-roundtrip": StardewBrowser.Testing.AdvancedActions.Prepare(); break;
        }
        SaveCurrent();
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
    if (id == "tool-upgrades") state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.ToolUpgradeActions.Run());
    if (id == "tool-upgrades")
    {
        Reload();
        StardewBrowser.Testing.ToolUpgradeActions.OpenShop();
        StardewBrowser.Testing.ToolUpgradeActions.Purchase();
        DismissToolDialogue();
        ToolNight(6);
        state["afterFirstNight"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.ToolUpgradeActions.Read());
        Reload();
        state["afterFirstReload"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.ToolUpgradeActions.Read());
        ToolNight(7);
        state["afterSecondNight"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.ToolUpgradeActions.Read());
        string readyId = "tool-upgrades-ready";
        string readyFixture = Path.Combine(Path.GetDirectoryName(report)!, "fixtures", readyId, slot);
        Directory.CreateDirectory(readyFixture);
        foreach (string name in new[] { slot, "SaveGameInfo" }) File.Copy(Path.Combine(saved, name), Path.Combine(readyFixture, name), true);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(report)!, "fixtures", readyId + ".json"),
            JsonSerializer.Serialize(new { gameVersion = typeof(Game1).Assembly.GetName().Version!.ToString(),
                scenario = new { id = readyId, saveFiles = Directory.GetFiles(readyFixture)
                    .Select(file => new { name = Path.GetFileName(file), bytes = new FileInfo(file).Length }).ToArray(),
                    observations = StardewBrowser.Testing.ToolUpgradeActions.Read() } }));
        var counter = StardewBrowser.Testing.ToolUpgradeActions.Counter();
        Game1.warpFarmer("Blacksmith", counter.X, counter.Y + 1, false);
        Until(() => Game1.currentLocation.Name == "Blacksmith" && !Game1.isWarping && Game1.player.CanMove, "visit original blacksmith");
        if (!StardewBrowser.Testing.ToolUpgradeActions.Collect()) throw new InvalidOperationException("Original blacksmith did not return the tool.");
        DismissToolDialogue();
        Game1.warpFarmer("FarmHouse", 7, 8, false);
        Until(() => Game1.currentLocation.Name == "FarmHouse" && !Game1.isWarping && Game1.player.CanMove, "return home with upgraded tool");
        ToolNight(8);
        Reload();
        state["afterCollectedReload"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.ToolUpgradeActions.Read());
    }
    if (id == "recipes-machines") state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.ProductionActions.Run());
    if (id == "island-qi-perfection") state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.IslandActions.Run());
    if (id == "original-minigames") state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.MinigameActions.Run());
    if (id == "festivals-events-movies")
    {
        StardewBrowser.Testing.CalendarActions.BeginFestival();
        Until(StardewBrowser.Testing.CalendarActions.FestivalReady, "finish original Egg Festival setup");
        state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.CalendarActions.Run());
    }
    if (id is "community-center" or "joja-orders-museum") state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.StoryActions.Run(id));
    if (id == "museum-quests")
    {
        var museum = StardewBrowser.Testing.MuseumActions.Museum;
        var exit = museum.warps.First(warp => warp.TargetName == "Town");
        GoToProgressLocation(museum.Name, exit.X, exit.Y - 1);
        StardewBrowser.Testing.MuseumActions.Open();
        Until(() => Game1.activeClickableMenu is MuseumMenu { fadeTimer: <= 0 }, "open original museum donation menu");
        StardewBrowser.Testing.MuseumActions.Donate();
        state["afterDonation"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.MuseumActions.Read());
        StardewBrowser.Testing.MuseumActions.Close();
        Until(() => Game1.activeClickableMenu == null && Game1.player.CanMove, "close original museum donation menu");
        StardewBrowser.Testing.MuseumActions.ClaimQuestReward();
        Until(() => Game1.activeClickableMenu == null && Game1.player.CanMove, "close original quest journal");
        state["afterReward"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.MuseumActions.Read());
        GoToProgressLocation("FarmHouse", 7, 8);
        StardewBrowser.Testing.AdvancedActions.BeginSleep();
        bool nightMenuSeen = false;
        Until(() => {
            nightMenuSeen |= Game1.activeClickableMenu is SaveGameMenu;
            return nightMenuSeen && Game1.dayOfMonth == 6 && taskField.GetValue(null) == null
                && !Game1.showingEndOfNightStuff && !Game1.game1.IsSaving && Game1.player.CanMove
                && Game1.activeClickableMenu == null && Game1.morningQueue.Count == 0;
        }, "complete original museum overnight");
        Reload();
        state["afterReload"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.MuseumActions.Read());
    }
    if (id == "skills-mastery-achievements") state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.ProgressionActions.Run());
    if (id == "skills-mastery-achievements") state["professionChoices"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.ProgressionActions.ChooseProfessions());
    if (id == "skills-mastery-achievements")
    {
        StardewBrowser.Testing.ProgressionActions.EarnRemainingMastery();
        GoToProgressLocation("MasteryCave", 7, 11);
        foreach (int skill in new[] { 0, 1, 2, 4 })
        {
            StardewBrowser.Testing.ProgressionActions.ClaimMastery(skill);
            Until(() => Game1.activeClickableMenu == null, "close original mastery reward menu");
        }
        state["afterAllMastery"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.ProgressionActions.Read());
        Until(() => Game1.player.CanMove, "finish original mastery celebration");
        GoToProgressLocation("FarmHouse", 7, 8);
        StardewBrowser.Testing.AdvancedActions.BeginSleep();
        bool nightMenuSeen = false;
        Until(() => {
            nightMenuSeen |= Game1.activeClickableMenu is LevelUpMenu or SaveGameMenu;
            return nightMenuSeen && Game1.dayOfMonth == 2 && taskField.GetValue(null) == null
                && !Game1.showingEndOfNightStuff && !Game1.game1.IsSaving && Game1.player.CanMove
                && Game1.activeClickableMenu == null && Game1.morningQueue.Count == 0;
        }, "complete original progression overnight", acknowledgeLevelNotices: true);
        Reload();
        state["afterProgressReload"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.ProgressionActions.Read());
    }
    if (id == "characters-family")
    {
        state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.FamilyActions.Run());
        Reload();
        var linus = Game1.getCharacterFromName("Linus");
        GoToProgressLocation(linus.currentLocation.NameOrUniqueName, (int)linus.Tile.X, (int)linus.Tile.Y + 1);
        StardewBrowser.Testing.FamilyActions.GiveHeldGift();
        DismissToolDialogue();
        state["afterNormalGift"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.FamilyActions.Read());
        GoToProgressLocation("FarmHouse", 7, 8);
        state["bedRoute"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.FamilyActions.BedRoute()
            .Select(point => new { x = point.X, y = point.Y }).ToArray());
        StardewBrowser.Testing.AdvancedActions.BeginSleep();
        bool nightMenuSeen = false;
        Until(() => {
            nightMenuSeen |= Game1.activeClickableMenu is SaveGameMenu;
            return nightMenuSeen && Game1.dayOfMonth == 6 && taskField.GetValue(null) == null
                && !Game1.showingEndOfNightStuff && !Game1.game1.IsSaving && Game1.player.CanMove
                && Game1.activeClickableMenu == null && Game1.morningQueue.Count == 0;
        }, "complete original family overnight");
        Reload();
        state["afterFamilyReload"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.FamilyActions.Read());
    }
    if (id == "combat-dungeons") state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.CombatActions.Run());
    if (id == "fishing-gathering") state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.FishingActions.Run());
    if (id == "fishing-gathering")
    {
        var shore = StardewBrowser.Testing.FishingActions.ShoreSpot();
        GoToProgressLocation("Beach", (int)shore.X, (int)shore.Y);
        StardewBrowser.Testing.FishingActions.Harvest();
        state["afterHarvest"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.FishingActions.Read());
        StardewBrowser.Testing.FishingActions.Rebait();
        state["afterRebait"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.FishingActions.Read());
        Until(() => Game1.player.CanMove, "finish original crab pot harvest animation");
        GoToProgressLocation("FarmHouse", 7, 8);
        StardewBrowser.Testing.AdvancedActions.BeginSleep();
        bool nightMenuSeen = false;
        Until(() => {
            nightMenuSeen |= Game1.activeClickableMenu is SaveGameMenu;
            return nightMenuSeen && Game1.dayOfMonth == 2 && taskField.GetValue(null) == null
                && !Game1.showingEndOfNightStuff && !Game1.game1.IsSaving && Game1.player.CanMove
                && Game1.activeClickableMenu == null && Game1.morningQueue.Count == 0;
        }, "complete original trap overnight");
        Reload();
        state["afterReload"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.FishingActions.Read());
        var pondSpot = StardewBrowser.Testing.FishingActions.PondSpot();
        GoToProgressLocation("Farm", (int)pondSpot.Shore.X, (int)pondSpot.Shore.Y);
        StardewBrowser.Testing.FishingActions.HarvestPond();
        state["afterPondHarvest"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.FishingActions.Read());
        GoToProgressLocation("FarmHouse", 7, 8);
        StardewBrowser.Testing.AdvancedActions.BeginSleep();
        nightMenuSeen = false;
        Until(() => {
            nightMenuSeen |= Game1.activeClickableMenu is SaveGameMenu;
            return nightMenuSeen && Game1.dayOfMonth == 3 && taskField.GetValue(null) == null
                && !Game1.showingEndOfNightStuff && !Game1.game1.IsSaving && Game1.player.CanMove
                && Game1.activeClickableMenu == null && Game1.morningQueue.Count == 0;
        }, "complete original pond harvest overnight");
        Reload();
        state["afterPondReload"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.FishingActions.Read());
    }
    if (id == "fishing-cast")
    {
        var shore = StardewBrowser.Testing.FishingCastActions.Shore();
        GoToProgressLocation("Town", (int)shore.X, (int)shore.Y);
        state["afterCast"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.FishingCastActions.Run(() => {
            NativeWindow.Frame(runner); Thread.Sleep(1);
        }));
        state["afterCatch"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.FishingCastActions.ReadState());
        GoToProgressLocation("FarmHouse", 7, 8);
        StardewBrowser.Testing.AdvancedActions.BeginSleep();
        bool nightMenuSeen = false;
        Until(() => {
            nightMenuSeen |= Game1.activeClickableMenu is SaveGameMenu;
            return nightMenuSeen && Game1.dayOfMonth == 2 && taskField.GetValue(null) == null
                && !Game1.showingEndOfNightStuff && !Game1.game1.IsSaving && Game1.player.CanMove
                && Game1.activeClickableMenu == null && Game1.morningQueue.Count == 0;
        }, "complete original caught-fish overnight");
        Reload();
        state["afterReload"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.FishingCastActions.ReadState());
    }
    if (id == "animals-buildings")
    {
        GoToFarm();
        state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.AnimalActions.Run());
    }
    if (id == "tailoring-automation-decoration") state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.DecorationActions.Run());
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
    if (id == "text-sign-clipboard")
    {
        state["observations"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.TextSignActions.Run());
        SaveCurrent();
        Reload();
        state["afterActionReload"] = JsonSerializer.SerializeToElement(StardewBrowser.Testing.TextSignActions.Read());
    }
    return state;


    void DismissToolDialogue()
    {
        Until(() => {
            if (Game1.activeClickableMenu is DialogueBox dialogue && !dialogue.transitioning && dialogue.safetyTimer <= 0
                && dialogue.characterIndexInDialogue >= dialogue.getCurrentString().Length - 1)
                dialogue.receiveLeftClick(0, 0);
            return Game1.player.CanMove && Game1.activeClickableMenu == null;
        }, "finish original tool dialogue/animation");
    }

    void ToolNight(int day)
    {
        StardewBrowser.Testing.AdvancedActions.BeginSleep();
        bool nightMenuSeen = false;
        Until(() => {
            nightMenuSeen |= Game1.activeClickableMenu is ShippingMenu or SaveGameMenu;
            return nightMenuSeen && Game1.dayOfMonth == day && taskField.GetValue(null) == null
                && !Game1.showingEndOfNightStuff && !Game1.game1.IsSaving && Game1.morningQueue.Count == 0
                && Game1.player.CanMove && Game1.activeClickableMenu == null;
        }, "complete original tool upgrade overnight");
    }

    void GoToFarm()
    {
        Game1.warpFarmer("Farm", 62, 15, false);
        Until(() => {
            if (Game1.eventUp)
                throw new InvalidOperationException("Unexpected event before construction: " + Game1.currentLocation.currentEvent?.id);
            return Game1.currentLocation.Name == "Farm" && !Game1.isWarping && Game1.player.CanMove
                && Game1.activeClickableMenu == null;
        }, "warp to original farm for construction");
    }

    void SaveCurrent()
    {
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

    void GoToProgressLocation(string location, int x, int y)
    {
        Game1.warpFarmer(location, x, y, false);
        Until(() => Game1.currentLocation.Name == location && !Game1.isWarping
            && Game1.player.CanMove && Game1.activeClickableMenu == null, "warp to original " + location);
    }

    void Until(Func<bool> done, string action, bool acknowledgeLevelNotices = false)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!done())
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Reference could not " + action
                + $"; location={Game1.currentLocation?.Name}, mode={Game1.gameMode}, loading={SaveGame.IsProcessing},"
                + $" warp={Game1.isWarping}, canMove={Game1.player.CanMove}, active={runner.IsActive},"
                + $" event={Game1.eventUp}, menu={Game1.activeClickableMenu?.GetType().Name}");
            NativeWindow.Frame(runner);
            if (Game1.activeClickableMenu is ShippingMenu shipping && shipping.CanReceiveInput())
                shipping.receiveLeftClick(shipping.okButton.bounds.Center.X, shipping.okButton.bounds.Center.Y);
            if (acknowledgeLevelNotices && Game1.activeClickableMenu is LevelUpMenu level && level.CanReceiveInput())
                level.okButtonClicked();
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
