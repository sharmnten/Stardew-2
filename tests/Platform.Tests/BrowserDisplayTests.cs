using Microsoft.Xna.Framework;
using StardewBrowser.Framework.Graphics;
using Xunit;

public sealed class BrowserDisplayTests
{
    [Fact]
    public void OriginalDisplayValuesFollowTheBrowserViewport()
    {
        Point size = new(960, 596);
        BrowserDisplay.Configure(() => size);
        var mode = BrowserDisplay.CurrentMode();
        Assert.Equal(960, mode.Width);
        Assert.Equal(596, mode.Height);
        size = new(1280, 720);
        mode = BrowserDisplay.CurrentMode();
        Assert.Equal(1280, mode.Width);
        Assert.Equal(720, mode.Height);
    }
}
