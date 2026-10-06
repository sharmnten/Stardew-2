using System.IO.Compression;
using StardewBrowser.Platform.Storage;
using Xunit;

public sealed class SaveArchiveTests
{
    [Fact]
    public void ExportedArchiveCanBeReimportedWithoutChangingOriginalBytes()
    {
        var files = new Dictionary<string, byte[]> { ["Farm_123"] = [60, 0, 255, 62], ["SaveGameInfo"] = [9, 8, 7] };
        var restored = SaveArchive.Read(SaveArchive.Create("Farm_123", files));
        Assert.Equal(files["Farm_123"], restored["Farm_123"]);
        Assert.Equal(files["SaveGameInfo"], restored["SaveGameInfo"]);
    }

    [Theory]
    [InlineData("../Farm_123")]
    [InlineData("Farm_123/nested/Farm_123")]
    [InlineData("Other/Farm_123")]
    public void InvalidArchivePathsAreRejectedBeforeImport(string path)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var entry = zip.CreateEntry(path).Open()) entry.Write([1]);
            using (var entry = zip.CreateEntry("Farm_123/SaveGameInfo").Open()) entry.Write([2]);
        }
        Assert.Throws<InvalidDataException>(() => SaveArchive.Read(stream.ToArray()));
    }
}
