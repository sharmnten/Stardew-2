using Microsoft.Xna.Framework;
using StardewBrowser.Platform.Input;
using Xunit;

public sealed class InputTests
{
    [Fact]
    public void LetterboxedPointerMapsToTheSameOriginalGamePixel()
    {
        var layout = new PointerLayout(1280, 720, 100, 200, 640, 360);
        Assert.Equal(new Point(640, 360), layout.ToLogical(420, 380));
        Assert.Equal(new Point(-20, -20), layout.ToLogical(90, 190));
        Assert.Equal(new Point(420, 380), layout.ToClient(640, 360));
    }
}
