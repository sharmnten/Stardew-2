using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Menus;

namespace StardewBrowser.Testing;

internal static class AnimalActions
{
    internal const string FixtureKey = "StardewBrowser.AnimalFixture";
    private static readonly FieldInfo MunchingTimer = typeof(Horse).GetField("munchingCarrotTimer", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal static bool Munching => (int)MunchingTimer.GetValue(Horse)! > 0;
    private static readonly Vector2 CoopTile = new(20, 20), BarnTile = new(35, 20), StableTile = new(40, 30);

    internal static void Prepare()
    {
        Game1.player.modData[FixtureKey] = "1";
        var farm = Game1.getFarm();
        foreach (var tile in new[] { CoopTile, BarnTile, StableTile })
        {
            var area = new Rectangle((int)tile.X * 64, (int)tile.Y * 64, 9 * 64, 7 * 64);
            for (int x = (int)tile.X; x < tile.X + 9; x++)
            for (int y = (int)tile.Y; y < tile.Y + 7; y++)
            {
                farm.objects.Remove(new Vector2(x, y));
                farm.terrainFeatures.Remove(new Vector2(x, y));
            }
            foreach (var clump in farm.resourceClumps.Where(clump => clump.getBoundingBox().Intersects(area)).ToArray())
                farm.resourceClumps.Remove(clump);
        }
        if (!farm.buildStructure("Coop", CoopTile, Game1.player, out var coop))
            throw new InvalidOperationException("Original fixture coop site is not buildable.");
        coop.FinishConstruction();
        AddAnimal("White Chicken", 12345, coop);
        farm.characters.Add(new Pet(60, 17, "0", "Dog") { Name = "PortDog", currentLocation = farm });
        Game1.player.mailReceived.Add("MarniePetAdoption");
        uint earnings = Game1.player.totalMoneyEarned;
        Game1.player.Money = 40000;
        // This fixture grants starting capital, rather than earning shipping income.
        Game1.player.totalMoneyEarned = earnings;
        for (int i = 0; i < Game1.player.Items.Count; i++) Game1.player.Items[i] = null;
        Game1.player.Items[0] = ItemRegistry.Create("(O)388", 999);
        Game1.player.Items[1] = ItemRegistry.Create("(O)390", 999);
        Game1.player.Items[2] = ItemRegistry.Create("(H)0");
        Game1.player.Items[3] = ItemRegistry.Create("(O)Carrot", 2);
    }

    internal static object Run()
    {
        var farm = Game1.getFarm();
        FarmAnimal chicken = farm.getAllFarmAnimals().Single(animal => animal.myID.Value == 12345);
        chicken.pet(Game1.player);
        chicken.dayUpdate(chicken.homeInterior);
        var chickenResult = new { friendship = chicken.friendshipTowardFarmer.Value, age = chicken.age.Value,
            produce = chicken.homeInterior.objects.Values.Where(item => item.QualifiedItemId == "(O)176")
                .Select(item => new { id = item.QualifiedItemId, quality = item.Quality, stack = item.Stack }).ToArray() };
        var carpenter = new CarpenterMenu(Game1.builder_robin, farm);
        carpenter.SetNewActiveBlueprint(carpenter.Blueprints.Single(blueprint => blueprint.Id == "Barn"));
        bool couldAfford = carpenter.DoesFarmerHaveEnoughResourcesToBuild();
        int money = Game1.player.Money;
        bool built = farm.buildStructure("Barn", BarnTile, Game1.player, out var barn);
        if (!built) throw new InvalidOperationException("Original barn placement failed.");
        carpenter.ConsumeResources();
        int cost = money - Game1.player.Money, initialDays = barn.daysOfConstructionLeft.Value;
        for (int day = 2; day <= 4; day++) barn.dayUpdate(day);
        var construction = new { built, couldAfford, cost, initialDays, completedDays = barn.daysOfConstructionLeft.Value };
        carpenter = new CarpenterMenu(Game1.builder_robin, farm);
        carpenter.SetNewActiveBlueprint(carpenter.Blueprints.Single(blueprint => blueprint.Id == "Big Barn"));
        money = Game1.player.Money;
        carpenter.ConsumeResources();
        barn.upgradeName.Value = carpenter.Blueprint.Id;
        barn.daysUntilUpgrade.Value = carpenter.Blueprint.BuildDays;
        for (int day = 5; day <= 6; day++) barn.dayUpdate(day);
        var upgrade = new { type = barn.buildingType.Value, days = barn.daysUntilUpgrade.Value,
            capacity = ((AnimalHouse)barn.GetIndoors()).animalLimit.Value, cost = money - Game1.player.Money };
        FarmAnimal cow = AddAnimal("White Cow", 12346, barn);
        cow.pet(Game1.player);
        cow.dayUpdate(cow.homeInterior);
        var cowResult = new { friendship = cow.friendshipTowardFarmer.Value, produce = cow.currentProduce.Value,
            quality = cow.produceQuality.Value, harvest = cow.GetHarvestType()?.ToString() };
        Pet pet = Utility.getAllPets().Single(pet => pet.Name == "PortDog");
        bool petted = pet.checkAction(Game1.player, pet.currentLocation);
        pet.mutex.Update(pet.currentLocation);
        var petResult = new { petted, friendship = pet.friendshipTowardFarmer.Value, timesPet = pet.timesPet.Value };
        bool stableBuilt = farm.buildStructure("Stable", StableTile, Game1.player, out var stable);
        if (!stableBuilt) throw new InvalidOperationException("Original stable placement failed.");
        for (int day = 7; day <= 8; day++) stable.dayUpdate(day);
        var horse = ((Stable)stable).getStableHorse();
        return new { construction, upgrade, chicken = chickenResult, cow = cowResult, pet = petResult,
            stable = new { built = stableBuilt, days = stable.daysOfConstructionLeft.Value,
                horseOwned = horse != null && horse.ownerId.Value == Game1.player.UniqueMultiplayerID } };
    }

    internal static Stable Stable => Game1.getFarm().buildings.OfType<Stable>().Single();
    internal static Horse Horse => Game1.player.mount ?? Stable.getStableHorse()
        ?? throw new InvalidOperationException("The original stable has no horse.");

    internal static Point HorseStand() => new(Stable.tileX.Value + 1, Stable.tileY.Value + 2);
    internal static object HorseInteraction()
    {
        var box = Horse.GetBoundingBox();
        return new { x = box.Center.X, y = box.Center.Y };
    }

    // Observe animal identities, care and mount state without including wandering positions.
    internal static object Read() => new {
        day = Game1.dayOfMonth, money = Game1.player.Money,
        carrots = Game1.player.Items.Where(item => item?.QualifiedItemId == "(O)Carrot").Sum(item => item.Stack),
        buildings = Game1.getFarm().buildings.Where(b => b is Stable || b.GetIndoors() is AnimalHouse)
            .OrderBy(b => b.buildingType.Value).Select(b => new {
                type = b.buildingType.Value, x = b.tileX.Value, y = b.tileY.Value,
                construction = b.daysOfConstructionLeft.Value, upgrade = b.daysUntilUpgrade.Value,
                capacity = b.GetIndoors() is AnimalHouse house ? house.animalLimit.Value : 0 }).ToArray(),
        animals = Game1.getFarm().getAllFarmAnimals().OrderBy(a => a.myID.Value).Select(a => new {
            id = a.myID.Value, type = a.type.Value, age = a.age.Value,
            friendship = a.friendshipTowardFarmer.Value, home = a.home.buildingType.Value }).ToArray(),
        horse = new { name = Horse.Name, farmerName = Game1.player.horseName.Value,
            hat = Horse.hat.Value?.QualifiedItemId, ateCarrotToday = Horse.ateCarrotToday,
            ownerMatches = Horse.ownerId.Value == Game1.player.UniqueMultiplayerID,
            stableMatches = Horse.HorseId == Stable.HorseId,
            mounted = Game1.player.mount == Horse, mounting = Horse.mounting.Value, dismounting = Horse.dismounting.Value }
    };

    // Native reference replay only. The browser uses DOM input and the original NamingMenu.
    internal static object Ride(Action frame)
    {
        var savedInput = Game1.input;
        bool savedGamepad = Game1.options.gamepadControls;
        var input = new RideInput();
        void Until(Func<bool> done, string stage)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!done())
            {
                if (timer.Elapsed > TimeSpan.FromSeconds(20))
                    throw new TimeoutException("Original horse stalled at " + stage
                        + $"; mounted={Game1.player.mount != null}, mounting={Horse.mounting.Value}, rider={Horse.rider != null}, lock={Horse.mutex.IsLockHeld()}, canMove={Game1.player.CanMove}");
                frame();
            }
        }
        try
        {
            Game1.input = input;
            Game1.options.gamepadControls = false;
            if (!Horse.checkAction(Game1.player, Game1.currentLocation))
                throw new InvalidOperationException("Original horse did not accept naming interaction.");
            Until(() => Game1.activeClickableMenu is NamingMenu, "naming prompt");
            var menu = (NamingMenu)Game1.activeClickableMenu;
            while (menu.textBox.Text.Length > 0) menu.textBox.RecieveCommandInput('\b');
            menu.textBox.RecieveTextInput("PortHorse");
            menu.receiveLeftClick(menu.doneNamingButton.bounds.Center.X, menu.doneNamingButton.bounds.Center.Y);
            Until(() => Game1.activeClickableMenu == null && Game1.player.CanMove, "finish naming");
            // Let the original mutex observe the naming lock release before a new request.
            frame();
            Game1.player.CurrentToolIndex = 2;
            Horse.checkAction(Game1.player, Game1.currentLocation);
            Until(() => Horse.hat.Value != null, "equipping the hat");
            frame();
            Game1.player.CurrentToolIndex = 3;
            Horse.checkAction(Game1.player, Game1.currentLocation);
            Until(() => Horse.ateCarrotToday && !Munching, "feeding the carrot");
            frame();
            Horse.checkAction(Game1.player, Game1.currentLocation);
            Until(() => Game1.player.mount != null && !Horse.mounting.Value && Game1.player.CanMove, "mounting");
            float y = Game1.player.Position.Y;
            input.Keys = [Keys.S];
            Until(() => Game1.player.Position.Y > y + 64, "riding movement");
            input.Keys = [];
            Until(() => !Game1.oldKBState.IsKeyDown(Keys.S), "release movement");
            bool moved = Game1.player.Position.Y > y;
            bool mounted = Game1.player.mount != null;
            input.Keys = [Keys.X];
            Until(() => Game1.player.mount == null && !Horse.dismounting.Value && Game1.player.CanMove, "dismounting");
            input.Keys = [];
            Until(() => !Game1.oldKBState.IsKeyDown(Keys.X), "release action");
            return new { moved, mounted };
        }
        finally { Game1.input = savedInput; Game1.options.gamepadControls = savedGamepad; }
    }

    private sealed class RideInput : InputState
    {
        internal Keys[] Keys = [];
        public override KeyboardState GetKeyboardState() => new(Keys);
        public override MouseState GetMouseState() => new(0, 0, 0,
            ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        public override GamePadState GetGamePadState() => default;
    }

    private static FarmAnimal AddAnimal(string type, long id, Building building)
    {
        var home = (AnimalHouse)building.GetIndoors();
        var animal = new FarmAnimal(type, id, Game1.player.UniqueMultiplayerID) { home = building, currentLocation = home };
        animal.age.Value = animal.GetAnimalData().DaysToMature;
        animal.fullness.Value = 255;
        animal.happiness.Value = 255;
        animal.daysSinceLastLay.Value = 1;
        home.animals.Add(id, animal);
        home.animalsThatLiveHere.Add(id);
        return animal;
    }
}
