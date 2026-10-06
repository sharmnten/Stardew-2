using Microsoft.Xna.Framework;
using StardewBrowser.Framework.Graphics;
using Xunit;

public sealed class TextureLayoutTests
{
    [Fact]
    public void PackedOriginalDimensionsKeepImageAndAllocationSeparate()
    {
        var image = TextureLayout.FromPacked(0x001f0020, 0x00150018);
        Assert.Equal(31, image.Width);
        Assert.Equal(21, image.Height);
        Assert.Equal(32, image.ActualWidth);
        Assert.Equal(24, image.ActualHeight);
        Assert.Equal(new Rectangle(0, 0, 31, 21), image.Bounds);
        var normal = TextureLayout.FromPacked(256, 512);
        Assert.Equal(256, normal.Width);
        Assert.Equal(256, normal.ActualWidth);
        Assert.Equal(512, normal.Height);
    }
}
