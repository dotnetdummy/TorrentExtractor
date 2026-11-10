using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TorrentExtractor;

public static class DownloadSettle
{
    public static async Task<bool> WaitAsync(
        Func<PathSnapshot> read,
        TimeSpan interval,
        TimeSpan maxWait,
        Func<TimeSpan, CancellationToken, Task> delay,
        Action onWaiting,
        CancellationToken cancellationToken
    )
    {
        var deadline = Stopwatch.StartNew();
        var hasPrevious = false;
        long previousLength = 0;
        var previousWrite = default(DateTime);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = read();
            if (
                hasPrevious
                && current.Length == previousLength
                && current.NewestWriteTimeUtc == previousWrite
            )
            {
                return true;
            }

            if (deadline.Elapsed >= maxWait)
            {
                return false;
            }

            onWaiting();
            await delay(interval, cancellationToken);

            previousLength = current.Length;
            previousWrite = current.NewestWriteTimeUtc;
            hasPrevious = true;
        }
    }
}
