using StardewBrowser.Platform.Compatibility;
using Xunit;

public class LegacyEffectTests
{
    // Literal MGFX9 fixture: one pixel shader, named sampler, technique and pass.
    private static readonly byte[] Original = Convert.FromHexString(
        "4D474658090078563412" + "00" + "01" +
        "000D000000766F6964206D61696E28297B7D01000000000570735F7330FF0000" +
        "00" + "0101540001015000FF00000000");
    private static readonly byte[] Expected = Convert.FromHexString(
        "4D4746580A0078563412" + "00000000" + "01000000" +
        "000D000000766F6964206D61696E28297B7D01000000000570735F7330FF0000" +
        "00000000" + "0100000001540000000001000000015000000000FFFFFFFF00000000000000" + "4D474658");

    [Fact]
    public void ConvertsContainerWhilePreservingShaderAndSamplerMetadata() => Assert.Equal(Expected, LegacyEffect.Convert(Original));

    [Fact]
    public void RejectsAnUnsupportedShaderFormat()
    {
        var unsupported = (byte[])Original.Clone();
        unsupported[4] = 8;
        Assert.Throws<NotSupportedException>(() => LegacyEffect.Convert(unsupported));
    }

    [Fact]
    public void RejectsTruncatedShaderData() => Assert.Throws<EndOfStreamException>(() => LegacyEffect.Convert(Original[..20]));
}
