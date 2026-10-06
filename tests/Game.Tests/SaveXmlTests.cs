using System.IO.Compression;
using System.Text;
using StardewBrowser.GameStorage;
using Xunit;

public sealed class SaveXmlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalSaveXmlAcceptsPlainAndLegacyZlibStreams(bool compressed)
    {
        byte[] bytes = Encoding.UTF8.GetBytes("<SaveGame><dayOfMonth>2</dayOfMonth></SaveGame>");
        if (compressed)
        {
            using var output = new MemoryStream();
            using (var zlib = new ZLibStream(output, CompressionMode.Compress, leaveOpen: true)) zlib.Write(bytes);
            bytes = output.ToArray();
        }
        using var reader = BrowserSaveBridge.OpenXmlReader(bytes);
        reader.MoveToContent();
        Assert.Equal("SaveGame", reader.Name);
        Assert.True(reader.ReadToDescendant("dayOfMonth"));
        Assert.Equal(2, reader.ReadElementContentAsInt());
    }
}
