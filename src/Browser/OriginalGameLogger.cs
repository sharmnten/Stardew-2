using StardewValley.Logging;

namespace StardewBrowser.Browser;

/// <summary>Expose errors the original release runtime catches internally.</summary>
internal sealed class OriginalGameLogger(Action<string> errorOutput, Action<string> warningOutput) : IGameLogger
{
    public void Verbose(string message) { }
    public void Debug(string message) { }
    public void Info(string message) { }
    public void Warn(string message) => warningOutput(message);
    public void Error(string error, Exception? exception = null) =>
        errorOutput(exception == null ? error : error + Environment.NewLine + exception);
}
