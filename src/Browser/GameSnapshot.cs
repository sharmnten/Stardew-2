using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewBrowser.Platform;

/// <summary>Read-only development instrumentation of the original runtime.</summary>
internal static class GameSnapshot
{
    private static readonly FieldInfo NewDayTask = typeof(Game1).GetField("_newDayTask", BindingFlags.Static | BindingFlags.NonPublic)!;
    internal static object Read()
    {
        IClickableMenu? menu = Game1.activeClickableMenu;
        if (menu is TitleMenu && TitleMenu.subMenu != null) menu = TitleMenu.subMenu;
        var player = Game1.player;
        var location = Game1.currentLocation;
        object? exit = null, bed = null, entrance = null;
        if (location is FarmHouse house)
        {
            var warp = house.warps.FirstOrDefault(warp => warp.TargetName == "Farm");
            if (warp != null) exit = new { x = warp.X * 64, y = (warp.Y - 1) * 64 };
            var spot = house.GetPlayerBedSpot();
            bed = new { x = spot.X * 64, y = spot.Y * 64 };
        }
        if (location is Farm farm)
        {
            var spot = farm.GetMainFarmHouseEntry();
            entrance = new { x = spot.X * 64, y = spot.Y * 64 };
        }
        return new {
            runtime = GameRunner.instance.GetType().FullName, day = Game1.dayOfMonth,
            active = GameRunner.instance.IsActive, ticks = Game1.ticks,
            viewport = new { x = Game1.viewport.X, y = Game1.viewport.Y, zoom = Game1.options.zoomLevel },
            language = LocalizedContentManager.CurrentLanguageCode.ToString(),
            warping = Game1.isWarping,
            preferences = Game1.activeClickableMenu is TitleMenu preferencesTitle ? new {
                startMuted = preferencesTitle.startupPreferences.startMuted,
                timesPlayed = preferencesTitle.startupPreferences.timesPlayed,
                language = preferencesTitle.startupPreferences.languageCode } : null,
            migration = Game1.gameMode == 3 && Game1.player?.isCustomized.Value == true ? new {
                lastSaveFix = (int)Game1.lastAppliedSaveFix,
                bundles = Game1.netWorldState.Value.BundleData } : null,
            overnight = NewDayTask.GetValue(null) != null || Game1.showingEndOfNightStuff || Game1.game1.IsSaving,
            save = SavedFiles(),
            lastTitleClick = BrowserDiagnostics.LastTitleClick,
            menu = new { type = menu?.GetType().Name, controls = Controls(menu),
                text = menu?.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(field => typeof(TextBox).IsAssignableFrom(field.FieldType))
                    .ToDictionary(field => field.Name, field => (field.GetValue(menu) as TextBox)?.Text),
                saves = menu is LoadGameMenu load ? load.MenuSlots.OfType<LoadGameMenu.SaveFileSlot>()
                    .Select(slot => new { slot = slot.Farmer.slotName, farmer = slot.Farmer.Name }).ToArray() : null,
                allowsInteraction = AllowsInteraction(menu) },
            player = player == null ? null : new { name = player.Name, farmName = player.farmName.Value,
                customized = player.isCustomized.Value, positionX = player.Position.X, positionY = player.Position.Y,
                stamina = player.Stamina, usingTool = player.UsingTool, canMove = player.CanMove,
                tool = player.CurrentTool?.GetType().Name, facing = player.FacingDirection,
                grab = new { x = player.GetGrabTile().X, y = player.GetGrabTile().Y },
                tile = new { x = player.Tile.X, y = player.Tile.Y } },
            location = location == null ? null : new { name = location.Name, exit, bed, houseEntrance = entrance },
            input = new { leftPressed = Game1.oldMouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed,
                mouseX = Game1.oldMouseState.X, mouseY = Game1.oldMouseState.Y,
                keys = Game1.oldKBState.GetPressedKeys().Select(key => key.ToString().ToLowerInvariant()).ToArray() },
            audioEngine = Game1.audioEngine?.GetType().Name
        };
    }

    private static object? SavedFiles()
    {
        if (Game1.dayOfMonth < 2 || Game1.player == null) return null;
        string slot = SaveGame.FilterFileName(Game1.GetSaveGameName()) + "_" + Game1.uniqueIDForThisGame;
        string directory = Path.Combine(StardewValley.Program.GetAppDataFolder("Saves", createIfMissing: false), slot);
        long Size(string name) => File.Exists(Path.Combine(directory, name)) ? new FileInfo(Path.Combine(directory, name)).Length : 0;
        return new { slot, mainBytes = Size(slot), farmerBytes = Size("SaveGameInfo") };
    }

    private static bool AllowsInteraction(IClickableMenu? menu) => menu switch {
        TitleMenu title => title.logoFadeTimer <= 0 && title.fadeFromWhiteTimer <= 0 && title.logoSwipeTimer <= 0
            && title.viewportDY == 0 && title.pauseBeforeViewportRiseTimer <= 0
            && (bool)typeof(TitleMenu).GetMethod("ShouldAllowInteraction", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(title, null)!,
        DialogueBox dialogue => !dialogue.transitioning && dialogue.safetyTimer <= 0
            && dialogue.characterIndexInDialogue >= dialogue.getCurrentString().Length - 1,
        _ => true
    };

    private static object[] Controls(IClickableMenu? menu)
    {
        if (menu == null) return [];
        var found = new List<object>();
        var seen = new HashSet<ClickableComponent>();
        float scale = Game1.options?.uiScale ?? 1;
        void Add(string name, ClickableComponent component)
        {
            if (!seen.Add(component)) return;
            var bounds = component.bounds;
            found.Add(new { name, x = bounds.X * scale, y = bounds.Y * scale, width = bounds.Width * scale, height = bounds.Height * scale });
        }
        foreach (var field in menu.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            object? value = field.GetValue(menu);
            if (value is ClickableComponent component) Add(field.Name, component);
            else if (value is IEnumerable collection && value is not string)
            {
                int index = 0;
                foreach (var item in collection)
                {
                    if (item is ClickableComponent control)
                    {
                        string name = menu is DialogueBox dialogue && field.Name == "responseCC" && index < dialogue.responses.Length
                            ? dialogue.responses[index].responseKey : string.IsNullOrEmpty(control.name) ? field.Name + index : control.name;
                        Add(name, control);
                    }
                    index++;
                }
            }
        }
        return found.ToArray();
    }
}
