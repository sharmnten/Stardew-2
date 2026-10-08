using StardewBrowser.Browser;
using Xunit;

public class OriginalGameLoggerTests
{
    [Fact]
    public void Caught_original_event_errors_reach_the_browser_error_channel_with_their_exception()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var logger = new OriginalGameLogger(errors.Add, warnings.Add);
        logger.Error("Error running event script on line 33", new FileNotFoundException("Tilesheets\\critters.xnb"));
        Assert.Single(errors);
        Assert.Contains("Error running event script on line 33", errors[0]);
        Assert.Contains("FileNotFoundException", errors[0]);
        Assert.Contains("Tilesheets\\critters.xnb", errors[0]);
        Assert.Empty(warnings);
    }
}
