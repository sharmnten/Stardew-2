using System.Reflection;
using Microsoft.Xna.Framework;
using StardewValley;
using Xunit;

public sealed class StaticSnapshotTests
{
    private static class Fields
    {
        public static int Day = 1;
        public static Vector2 Position = new(64, 128);
        public static List<string> Items = ["initial"];
        public static string? Optional;
    }

    [Fact]
    public void OriginalHolderRoundTripsValuesAndReferencesWithoutDynamicCodeGeneration()
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var fields = typeof(Fields).GetFields(BindingFlags.Public | BindingFlags.Static).ToList();
        var defaults = fields.Select(field => field.GetValue(null)).ToList();
        typeof(LocalMultiplayer).GetField("staticFields", flags)!.SetValue(null, fields);
        typeof(LocalMultiplayer).GetField("staticDefaults", flags)!.SetValue(null, defaults);
        typeof(LocalMultiplayer).GetMethod("GenerateDynamicMethodsForStatics", flags)!.Invoke(null, null);
        Assert.False(LocalMultiplayer.StaticVarHolderType.Assembly.IsDynamic);
        object holder = Activator.CreateInstance(LocalMultiplayer.StaticVarHolderType)!;
        LocalMultiplayer.StaticSetDefault(holder);
        var originalItems = Fields.Items;
        Fields.Day = 18;
        Fields.Position = new(512, 1024);
        Fields.Items = ["new"];
        Fields.Optional = "saved";
        LocalMultiplayer.StaticSave(holder);
        var savedItems = Fields.Items;
        Fields.Day = 90; Fields.Position = Vector2.Zero; Fields.Items = []; Fields.Optional = null;
        LocalMultiplayer.StaticLoad(holder);
        Assert.Equal(18, Fields.Day);
        Assert.Equal(new Vector2(512, 1024), Fields.Position);
        Assert.Same(savedItems, Fields.Items);
        Assert.Equal("saved", Fields.Optional);
        LocalMultiplayer.StaticSetDefault(holder);
        LocalMultiplayer.StaticLoad(holder);
        Assert.Equal(1, Fields.Day);
        Assert.Equal(new Vector2(64, 128), Fields.Position);
        Assert.Same(originalItems, Fields.Items);
        Assert.Null(Fields.Optional);
    }
}
