using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Menus;

namespace StardewBrowser.Testing;

internal static class AnimalActions
{
    private static readonly Vector2 CoopTile = new(20, 20), BarnTile = new(35, 20), StableTile = new(40, 30);

    internal static void Prepare()
    {
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
