using System.Net.Http.Json;
using System.Text.Json;
using StardewBrowser.Platform.Storage;
using StardewValley;
using StardewValley.Menus;

namespace StardewBrowser.Platform.Testing;

/// <summary>Development-only import of fixtures produced by the supplied desktop game.</summary>
internal sealed class ScenarioBridge(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim serial = new(1, 1);
    private string? loadedScenario;

    internal async Task LoadScenarioAsync(string scenarioId)
    {
        if (!Allowed.Contains(scenarioId)) throw new ArgumentException("Unknown reference scenario: " + scenarioId);
        await serial.WaitAsync();
        try
        {
            if (Game1.gameMode != 0 || Game1.activeClickableMenu is not TitleMenu)
            {
                var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Game1.ExitToTitle(() => returned.TrySetResult());
                await returned.Task.WaitAsync(TimeSpan.FromSeconds(60));
            }
            var report = await http.GetFromJsonAsync<ReferenceReport>("Fixtures/reference/" + scenarioId + ".json", Json)
                ?? throw new InvalidDataException("Missing original reference report.");
            if (report.GameVersion != "1.6.15.24356" || report.Scenario.Id != scenarioId)
                throw new InvalidDataException("Reference fixture identity does not match the supplied game.");
            string slot = report.Scenario.SaveFiles.Single(file => file.Name != "SaveGameInfo").Name;
            BrowserSaveStore.ValidateSlot(slot);
            var files = new Dictionary<string, byte[]>();
            foreach (var file in report.Scenario.SaveFiles)
            {
                if (file.Name != slot && file.Name != "SaveGameInfo") throw new InvalidDataException("Unexpected fixture file.");
                byte[] bytes = await http.GetByteArrayAsync("Fixtures/reference/" + scenarioId + "/" + slot + "/" + file.Name);
                if (bytes.Length != file.Bytes) throw new InvalidDataException("Reference fixture length mismatch.");
                files.Add(file.Name, bytes);
            }
            await BrowserPersistence.Current.Store.ImportAsync(files);
            // Use the original selection handler, including its title-menu cleanup.
            var title = (TitleMenu)Game1.activeClickableMenu;
            var menu = new LoadGameMenu();
            title.ForceSubmenu(menu);
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            LoadGameMenu.SaveFileSlot? selected;
            while ((selected = menu.MenuSlots.OfType<LoadGameMenu.SaveFileSlot>()
                .FirstOrDefault(candidate => candidate.Farmer.slotName == slot)) == null)
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Original Load menu did not find the reference save.");
                await Task.Delay(16);
            }
            selected.Activate();
            timeout.Restart();
            while (Game1.gameMode != 3 || SaveGame.IsProcessing || !Game1.player.CanMove || Game1.isWarping
                || Game1.activeClickableMenu != null)
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(90)) throw new TimeoutException("Original fixture load did not complete.");
                await Task.Delay(16);
            }
            loadedScenario = scenarioId;
        }
        finally { serial.Release(); }
    }

    internal async Task<string> RunActionAsync(string id)
    {
        if (id is "animals-horse" or "animals-home")
        {
            if (!Game1.player.modData.ContainsKey(StardewBrowser.Testing.AnimalActions.FixtureKey)
                || Game1.activeClickableMenu != null || Game1.player.mount != null)
                throw new InvalidOperationException("Load the livestock fixture and dismount before location setup.");
            bool visit = id == "animals-horse";
            var stand = visit ? StardewBrowser.Testing.AnimalActions.HorseStand() : new Microsoft.Xna.Framework.Point(7, 8);
            string target = visit ? "Farm" : "FarmHouse";
            Game1.warpFarmer(target, stand.X, stand.Y, false);
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (Game1.currentLocation.Name != target || Game1.isWarping || !Game1.player.CanMove
                || Game1.activeClickableMenu != null)
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Original livestock location warp did not finish.");
                await Task.Delay(16);
            }
            return "{}";
        }
        if (id is "minigames-kart" or "minigames-king" or "minigames-home")
        {
            if (!Game1.player.modData.ContainsKey(StardewBrowser.Testing.MinigameActions.FixtureKey)
                || Game1.currentMinigame != null || Game1.activeClickableMenu != null)
                throw new InvalidOperationException("Load the arcade fixture and finish its active game before opening the kart menu.");
            bool home = id == "minigames-home";
            var cabinet = home ? default : StardewBrowser.Testing.MinigameActions.Cabinet(id == "minigames-kart");
            string target = home ? "FarmHouse" : "Saloon";
            Game1.warpFarmer(target, home ? 7 : (int)cabinet.Stand.X, home ? 8 : (int)cabinet.Stand.Y, false);
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (Game1.currentLocation.Name != target || Game1.isWarping || !Game1.player.CanMove
                || Game1.activeClickableMenu != null)
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Original arcade cabinet warp did not finish.");
                await Task.Delay(16);
            }
            return JsonSerializer.Serialize(new { x = (int)cabinet.Target.X, y = (int)cabinet.Target.Y }, Json);
        }
        if (id is "fishing-cast-shore" or "fishing-cast-home")
        {
            if (!Game1.player.modData.ContainsKey(StardewBrowser.Testing.FishingCastActions.FixtureKey))
                throw new InvalidOperationException("Load the casting fixture before its location setup.");
            bool visit = id == "fishing-cast-shore";
            var shore = visit ? StardewBrowser.Testing.FishingCastActions.Shore() : new Microsoft.Xna.Framework.Vector2(7, 8);
            string target = visit ? "Town" : "FarmHouse";
            Game1.warpFarmer(target, (int)shore.X, (int)shore.Y, false);
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (Game1.currentLocation.Name != target || Game1.isWarping || !Game1.player.CanMove
                || Game1.activeClickableMenu != null)
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Original casting location warp did not finish.");
                await Task.Delay(16);
            }
            if (visit) Game1.player.faceDirection(2);
            return JsonSerializer.Serialize(new { x = Game1.player.Position.X + 32, y = Game1.player.Position.Y + 160 }, Json);
        }
        if (id is "fishing-shore" or "fishing-home" or "fishing-pond")
        {
            if (!Game1.player.modData.ContainsKey(StardewBrowser.Testing.FishingActions.FixtureKey))
                throw new InvalidOperationException("Load the fishing fixture before its location setup.");
            bool visit = id == "fishing-shore";
            bool pondVisit = id == "fishing-pond";
            var pondSpot = pondVisit ? StardewBrowser.Testing.FishingActions.PondSpot() : default;
            var shore = visit ? StardewBrowser.Testing.FishingActions.ShoreSpot()
                : pondVisit ? pondSpot.Shore : new Microsoft.Xna.Framework.Vector2(7, 8);
            string target = visit ? "Beach" : pondVisit ? "Farm" : "FarmHouse";
            Game1.warpFarmer(target, (int)shore.X, (int)shore.Y, false);
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (Game1.currentLocation.Name != target || Game1.isWarping || !Game1.player.CanMove
                || Game1.activeClickableMenu != null)
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Original fishing location warp did not finish.");
                await Task.Delay(16);
            }
            var tile = pondVisit ? pondSpot.Target : StardewBrowser.Testing.FishingActions.Pot.TileLocation;
            return JsonSerializer.Serialize(new { x = (int)tile.X, y = (int)tile.Y }, Json);
        }
        if (id is "museum-visit" or "museum-donate-menu" or "museum-home")
        {
            if (!Game1.player.modData.ContainsKey(StardewBrowser.Testing.MuseumActions.FixtureKey))
                throw new InvalidOperationException("Load the museum fixture before its UI setup.");
            var museum = StardewBrowser.Testing.MuseumActions.Museum;
            if (id == "museum-donate-menu")
            {
                if (Game1.currentLocation != museum) throw new InvalidOperationException("Visit the original museum first.");
                StardewBrowser.Testing.MuseumActions.Open();
                return "{}";
            }
            bool visit = id == "museum-visit";
            var exit = museum.warps.First(warp => warp.TargetName == "Town");
            string target = visit ? museum.Name : "FarmHouse";
            Game1.warpFarmer(target, visit ? exit.X : 7, visit ? exit.Y - 1 : 8, false);
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (Game1.currentLocation.Name != target || Game1.isWarping || !Game1.player.CanMove
                || Game1.activeClickableMenu != null)
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Original museum location warp did not finish.");
                await Task.Delay(16);
            }
            var spot = museum.getFreeDonationSpot();
            return JsonSerializer.Serialize(new { x = (int)spot.X, y = (int)spot.Y }, Json);
        }
        if (id is "family-visit-linus" or "family-home")
        {
            if (!Game1.player.modData.ContainsKey(StardewBrowser.Testing.FamilyActions.FixtureKey))
                throw new InvalidOperationException("Load the family fixture before its location setup.");
            bool visit = id == "family-visit-linus";
            var linus = Game1.getCharacterFromName("Linus");
            string target = visit ? linus.currentLocation.NameOrUniqueName : "FarmHouse";
            Game1.warpFarmer(target, visit ? (int)linus.Tile.X : 7, visit ? (int)linus.Tile.Y + 1 : 8, false);
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (Game1.currentLocation.NameOrUniqueName != target || Game1.isWarping || !Game1.player.CanMove
                || Game1.activeClickableMenu != null)
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Original family location warp did not finish.");
                await Task.Delay(16);
            }
            return visit ? "{}" : JsonSerializer.Serialize(StardewBrowser.Testing.FamilyActions.BedRoute()
                .Select(point => new { x = point.X, y = point.Y }).ToArray(), Json);
        }
        if (id is "mastery-prepare" or "progression-home")
        {
            if (!Game1.player.modData.ContainsKey(StardewBrowser.Testing.ProgressionActions.FixtureKey))
                throw new InvalidOperationException("Load the progression fixture before its mastery UI setup.");
            bool mastery = id == "mastery-prepare";
            if (mastery) StardewBrowser.Testing.ProgressionActions.EarnRemainingMastery();
            string target = mastery ? "MasteryCave" : "FarmHouse";
            Game1.warpFarmer(target, 7, mastery ? 11 : 8, false);
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (Game1.currentLocation.Name != target || Game1.isWarping || !Game1.player.CanMove
                || Game1.activeClickableMenu != null)
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Original progression location warp did not finish.");
                await Task.Delay(16);
            }
            return "{}";
        }
        if (id.StartsWith("mastery-", StringComparison.Ordinal))
        {
            if (loadedScenario != "skills-mastery-achievements") throw new InvalidOperationException("Load the progression fixture before mastery menus.");
            if (!int.TryParse(id[8..], out int skill) || skill is < 0 or > 4)
                throw new ArgumentException("Unknown mastery menu: " + id);
            StardewBrowser.Testing.ProgressionActions.ShowMastery(skill);
            return "{}";
        }
        if (id.StartsWith("profession-", StringComparison.Ordinal))
        {
            if (loadedScenario != "skills-mastery-achievements") throw new InvalidOperationException("Load the progression fixture before profession menus.");
            var parts = id.Split('-');
            if (parts.Length != 3 || !int.TryParse(parts[1], out int skill) || skill is < 0 or > 4
                || !int.TryParse(parts[2], out int level) || level is not (5 or 10))
                throw new ArgumentException("Unknown profession menu: " + id);
            StardewBrowser.Testing.ProgressionActions.ShowProfession(skill, level);
            return "{}";
        }
        if (id is "tool-upgrades-shop" or "tool-upgrades-visit" or "tool-upgrades-home")
        {
            if (!Game1.player.modData.ContainsKey(StardewBrowser.Testing.ToolUpgradeActions.FixtureKey))
                throw new InvalidOperationException("Load the tool-upgrade fixture before its UI setup.");
            if (id == "tool-upgrades-shop")
            {
                StardewBrowser.Testing.ToolUpgradeActions.OpenShop();
                return "{}";
            }
            bool visit = id == "tool-upgrades-visit";
            var counter = StardewBrowser.Testing.ToolUpgradeActions.Counter();
            string target = visit ? "Blacksmith" : "FarmHouse";
            Game1.warpFarmer(target, visit ? counter.X : 7, visit ? counter.Y + 1 : 8, false);
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (Game1.currentLocation.Name != target || Game1.isWarping || !Game1.player.CanMove
                || Game1.activeClickableMenu != null)
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Original tool upgrade location warp did not finish.");
                await Task.Delay(16);
            }
            return JsonSerializer.Serialize(new { x = counter.X, y = counter.Y }, Json);
        }
        if (loadedScenario != id)
            throw new InvalidOperationException("Load the matching original fixture before running its actions.");
        if (id == "animals-buildings")
        {
            Game1.warpFarmer("Farm", 62, 15, false);
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (Game1.currentLocation.Name != "Farm" || Game1.isWarping || !Game1.player.CanMove
                || Game1.activeClickableMenu != null)
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Original farm warp did not finish.");
                await Task.Delay(16);
            }
        }
        if (id == "festivals-events-movies")
        {
            StardewBrowser.Testing.CalendarActions.BeginFestival();
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (!StardewBrowser.Testing.CalendarActions.FestivalReady())
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Original festival setup did not finish.");
                await Task.Delay(16);
            }
        }
        object observations = id switch {
            "farming-season" => StardewBrowser.Testing.FarmingActions.Run(),
            "tool-upgrades" => StardewBrowser.Testing.ToolUpgradeActions.Run(),
            "inventory-economy" => StardewBrowser.Testing.EconomyActions.Run(),
            "recipes-machines" => StardewBrowser.Testing.ProductionActions.Run(),
            "tailoring-automation-decoration" => StardewBrowser.Testing.DecorationActions.Run(),
            "animals-buildings" => StardewBrowser.Testing.AnimalActions.Run(),
            "fishing-gathering" => StardewBrowser.Testing.FishingActions.Run(),
            "island-qi-perfection" => StardewBrowser.Testing.IslandActions.Run(),
            "original-minigames" => StardewBrowser.Testing.MinigameActions.Run(),
            "festivals-events-movies" => StardewBrowser.Testing.CalendarActions.Run(),
            "community-center" or "joja-orders-museum" => StardewBrowser.Testing.StoryActions.Run(id),
            "skills-mastery-achievements" => StardewBrowser.Testing.ProgressionActions.Run(),
            "characters-family" => StardewBrowser.Testing.FamilyActions.Run(),
            "combat-dungeons" => StardewBrowser.Testing.CombatActions.Run(),
            _ => throw new ArgumentException("This scenario has no method comparison: " + id)
        };
        if (id == "original-minigames") Game1.currentLocation.showPrairieKingMenu();
        return JsonSerializer.Serialize(observations, Json);
    }

    internal string Snapshot()
    {
        var farm = Game1.getFarm();
        return JsonSerializer.Serialize(new {
            day = Game1.dayOfMonth, farmId = Game1.GetFarmTypeID(), map = farm.mapPath.Value,
            farmer = new { name = Game1.player.Name, customized = Game1.player.isCustomized.Value, money = Game1.player.Money },
            buildings = farm.buildings.Select(building => new { type = building.buildingType.Value,
                x = building.tileX.Value, y = building.tileY.Value }).OrderBy(building => building.type).ToArray(),
            animals = farm.getAllFarmAnimals().Select(animal => new { type = animal.type.Value, name = animal.Name }).OrderBy(animal => animal.type).ToArray(),
            advanced = Game1.player.modData.ContainsKey(StardewBrowser.Testing.AdvancedActions.FixtureKey)
                ? StardewBrowser.Testing.AdvancedActions.Read() : null,
            professions = Game1.player.professions.Order().ToArray(),
            progression = Game1.player.modData.ContainsKey(StardewBrowser.Testing.ProgressionActions.FixtureKey)
                ? StardewBrowser.Testing.ProgressionActions.Read() : null,
            family = Game1.player.modData.ContainsKey(StardewBrowser.Testing.FamilyActions.FixtureKey)
                ? StardewBrowser.Testing.FamilyActions.Read() : null,
            museum = Game1.player.modData.ContainsKey(StardewBrowser.Testing.MuseumActions.FixtureKey)
                ? StardewBrowser.Testing.MuseumActions.Read() : null,
            fishing = Game1.player.modData.ContainsKey(StardewBrowser.Testing.FishingActions.FixtureKey)
                ? StardewBrowser.Testing.FishingActions.Read() : null,
            fishingCast = Game1.player.modData.ContainsKey(StardewBrowser.Testing.FishingCastActions.FixtureKey)
                ? StardewBrowser.Testing.FishingCastActions.Read() : null,
            kart = Game1.currentMinigame is StardewValley.Minigames.MineCart cart
                ? StardewBrowser.Testing.MinigameActions.ReadKart(cart) : null,
            livestock = Game1.player.modData.ContainsKey(StardewBrowser.Testing.AnimalActions.FixtureKey)
                && farm.buildings.OfType<StardewValley.Buildings.Stable>().Any()
                ? StardewBrowser.Testing.AnimalActions.Read() : null,
            horseInput = Game1.player.modData.ContainsKey(StardewBrowser.Testing.AnimalActions.FixtureKey)
                ? new { slot = Game1.player.CurrentToolIndex,
                    item = Game1.player.Items[Game1.player.CurrentToolIndex]?.QualifiedItemId,
                    rightDown = Game1.oldMouseState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed } : null,
            horseMunching = Game1.player.modData.ContainsKey(StardewBrowser.Testing.AnimalActions.FixtureKey)
                && farm.buildings.OfType<StardewValley.Buildings.Stable>().Any()
                && StardewBrowser.Testing.AnimalActions.Munching,
            horseInteraction = Game1.player.modData.ContainsKey(StardewBrowser.Testing.AnimalActions.FixtureKey)
                && farm.buildings.OfType<StardewValley.Buildings.Stable>().Any()
                ? StardewBrowser.Testing.AnimalActions.HorseInteraction() : null,
            savedKing = Game1.player.modData.ContainsKey(StardewBrowser.Testing.MinigameActions.FixtureKey)
                ? StardewBrowser.Testing.MinigameActions.ReadSavedKing() : null,
            linusPosition = Game1.player.modData.ContainsKey(StardewBrowser.Testing.FamilyActions.FixtureKey)
                ? new { x = Game1.getCharacterFromName("Linus").Position.X, y = Game1.getCharacterFromName("Linus").Position.Y } : null,
            morningQueueCount = Game1.morningQueue.Count,
            toolDiagnostics = Game1.player.modData.ContainsKey(StardewBrowser.Testing.ToolUpgradeActions.FixtureKey)
                ? StardewBrowser.Testing.ToolUpgradeActions.Diagnostics() : null,
            toolUpgrade = Game1.player.modData.ContainsKey(StardewBrowser.Testing.ToolUpgradeActions.FixtureKey)
                ? StardewBrowser.Testing.ToolUpgradeActions.Read() : null,
            locationCount = Game1.locations.Count,
            locations = Game1.locations.Select(location => location.NameOrUniqueName).Order().ToArray()
        }, Json);
    }

    private static readonly HashSet<string> Allowed = ["new-game-standard", "new-game-riverland", "new-game-forest",
        "new-game-hilltop", "new-game-wilderness", "new-game-four-corners", "new-game-beach", "new-game-meadowlands",
        "farming-season", "inventory-economy", "tool-upgrades", "tool-upgrades-ready", "recipes-machines", "advanced-desktop-roundtrip", "tailoring-automation-decoration", "text-sign-clipboard", "animals-buildings", "fishing-gathering", "fishing-cast", "combat-dungeons", "characters-family", "skills-mastery-achievements", "community-center", "joja-orders-museum", "museum-quests", "festivals-events-movies", "original-minigames", "island-qi-perfection"];
    private sealed record ReferenceReport(string GameVersion, ReferenceScenario Scenario);
    private sealed record ReferenceScenario(string Id, ReferenceFile[] SaveFiles);
    private sealed record ReferenceFile(string Name, long Bytes);
}
