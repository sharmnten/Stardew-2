using System.Globalization;

namespace StardewBrowser.Platform;

/// <summary>Advance an original iterator on successive browser turns without a worker thread.</summary>
public static class CooperativeOperation
{
    public static Task Run(IEnumerator<int> operation, Func<Task>? nextTurn = null, int? stopAt = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var completion = new TaskCompletionSource();
        _ = PumpAsync();
        return completion.Task;

        async Task PumpAsync()
        {
            try
            {
                using (operation)
                {
                    do {
                        await (nextTurn?.Invoke() ?? Task.Delay(1));
                        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    }
                    while (operation.MoveNext() && (stopAt == null || operation.Current < stopAt));
                }
                completion.SetResult();
            }
            catch (Exception error)
            {
                // Original Action tasks fault on a TaskCanceledException raised by
                // save CancelToTitle. Preserve that status for the original caller.
                completion.SetException(error);
            }
        }
    }
}
