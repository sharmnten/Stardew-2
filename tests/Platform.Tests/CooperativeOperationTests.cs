using StardewBrowser.Platform;
using System.Globalization;
using Xunit;

public sealed class CooperativeOperationTests
{
    [Fact]
    public async Task OriginalEnumeratorYieldsBetweenStepsAndRetainsOrder()
    {
        var trace = new List<int>();
        var turns = Enumerable.Range(0, 4).Select(_ => new TaskCompletionSource()).ToArray();
        var arrivals = Enumerable.Range(0, 4).Select(_ => new TaskCompletionSource()).ToArray();
        int requestedTurn = 0;
        bool disposed = false;
        IEnumerator<int> Original()
        {
            try
            {
                trace.Add(1); yield return 1;
                trace.Add(2); yield return 2;
                trace.Add(3);
            }
            finally { disposed = true; }
        }
        Task operation = CooperativeOperation.Run(Original(), () => {
            int turn = requestedTurn++;
            arrivals[turn].SetResult();
            return turns[turn].Task;
        });
        Assert.Empty(trace);
        Assert.False(operation.IsCompleted);
        for (int step = 0; step < 3; step++)
        {
            turns[step].SetResult();
            if (step < 2) await arrivals[step + 1].Task.WaitAsync(TimeSpan.FromSeconds(5));
            else await operation.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(Enumerable.Range(1, step + 1), trace);
        }
        await operation;
        Assert.True(disposed);
    }

    [Fact]
    public async Task SaveCompletionMarkerStopsBeforeAnyLaterIteratorWork()
    {
        bool disposed = false;
        IEnumerator<int> Original()
        {
            try { yield return 1; yield return 100; throw new InvalidOperationException("Past the original save marker"); }
            finally { disposed = true; }
        }
        await CooperativeOperation.Run(Original(), stopAt: 100);
        Assert.True(disposed);
    }

    [Fact]
    public async Task OriginalTaskCultureIsInvariantWithoutChangingTheCallersCulture()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        string? formatted = null;
        IEnumerator<int> Original()
        {
            formatted = 1.5.ToString("0.0");
            yield return 100;
        }
        await CooperativeOperation.Run(Original());
        Assert.Equal("1.5", formatted);
        Assert.Equal("de-DE", CultureInfo.CurrentCulture.Name);
    }

    [Fact]
    public async Task OriginalAbortRemainsFaultedAndDisposesTheEnumerator()
    {
        bool disposed = false;
        var abort = new TaskCanceledException("Original save canceled to title");
        IEnumerator<int> Original()
        {
            try { yield return 1; throw abort; }
            finally { disposed = true; }
        }
        Task operation = CooperativeOperation.Run(Original());
        Assert.Same(abort, await Assert.ThrowsAsync<TaskCanceledException>(() => operation));
        Assert.True(operation.IsFaulted);
        Assert.True(disposed);
    }
}
