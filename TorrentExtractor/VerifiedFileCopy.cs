using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TorrentExtractor;

public static class VerifiedFileCopy
{
    private const int BufferSize = 1024 * 1024;
    private const int DestinationAttempts = 3;

    public static Task<bool> CopyAsync(
        string sourcePath,
        string destinationPath,
        TimeSpan settleInterval,
        TimeSpan maxWait,
        ILogger logger,
        CancellationToken cancellationToken
    ) =>
        CopyAsync(
            sourcePath,
            destinationPath,
            settleInterval,
            maxWait,
            logger,
            cancellationToken,
            null
        );

    internal static async Task<bool> CopyAsync(
        string sourcePath,
        string destinationPath,
        TimeSpan settleInterval,
        TimeSpan maxWait,
        ILogger logger,
        CancellationToken cancellationToken,
        Func<string, CancellationToken, Task> afterFlush
    )
    {
        var deadline = Stopwatch.StartNew();
        var destinationFailures = 0;
        var settleFirst = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (settleFirst)
            {
                var remaining = maxWait - deadline.Elapsed;
                if (remaining < TimeSpan.Zero)
                {
                    remaining = TimeSpan.Zero;
                }

                var settled = await DownloadSettle.WaitAsync(
                    () => ReadSource(sourcePath),
                    settleInterval,
                    remaining,
                    Task.Delay,
                    () =>
                        logger.LogInformation(
                            "File '{SourcePath}' is still being copied. Waiting for {Interval} seconds...",
                            sourcePath,
                            settleInterval.TotalSeconds
                        ),
                    cancellationToken
                );

                if (!settled)
                {
                    logger.LogError(
                        "File '{SourcePath}' did not settle within {MaxSettleHours} hours. Skipping",
                        sourcePath,
                        maxWait.TotalHours
                    );
                    return false;
                }
            }

            switch (await AttemptAsync(sourcePath, destinationPath, afterFlush, cancellationToken))
            {
                case AttemptResult.Published:
                    return true;
                case AttemptResult.SourceChanged:
                    destinationFailures = 0;
                    settleFirst = true;
                    logger.LogInformation(
                        "Source '{SourcePath}' changed while copying to '{DestinationPath}'. Settling again...",
                        sourcePath,
                        destinationPath
                    );
                    break;
                case AttemptResult.DestinationMismatch:
                    destinationFailures++;
                    if (destinationFailures >= DestinationAttempts)
                    {
                        logger.LogError(
                            "An error occurred when copying '{SourcePath}' to '{DestinationPath}'. Cleaning up corrupted files...",
                            sourcePath,
                            destinationPath
                        );
                        return false;
                    }

                    logger.LogInformation(
                        "Copy of '{SourcePath}' to '{DestinationPath}' did not match after hashing. Retrying...",
                        sourcePath,
                        destinationPath
                    );
                    break;
            }
        }
    }

    private static async Task<AttemptResult> AttemptAsync(
        string sourcePath,
        string destinationPath,
        Func<string, CancellationToken, Task> afterFlush,
        CancellationToken cancellationToken
    )
    {
        var partialPath = destinationPath + ".partial";
        var published = false;
        try
        {
            var writtenHash = await CopyToPartialAsync(sourcePath, partialPath, cancellationToken);
            if (afterFlush != null)
            {
                await afterFlush(partialPath, cancellationToken);
            }

            var sourceHash = await HashFileAsync(sourcePath, cancellationToken);
            if (!HashesEqual(writtenHash, sourceHash))
            {
                return AttemptResult.SourceChanged;
            }

            var destinationHash = await HashFileAsync(partialPath, cancellationToken);
            if (!HashesEqual(writtenHash, destinationHash))
            {
                return AttemptResult.DestinationMismatch;
            }

            File.Move(partialPath, destinationPath, overwrite: true);
            published = true;
            return AttemptResult.Published;
        }
        finally
        {
            if (!published)
            {
                DeleteIfExists(partialPath);
            }
        }
    }

    private static async Task<byte[]> CopyToPartialAsync(
        string sourcePath,
        string partialPath,
        CancellationToken cancellationToken
    )
    {
        var buffer = new byte[BufferSize];
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        await using var reader = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );
        await using var writer = new FileStream(
            partialPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous
        );

        int numRead;
        while ((numRead = await reader.ReadAsync(buffer, cancellationToken)) != 0)
        {
            hasher.AppendData(buffer.AsSpan(0, numRead));
            await writer.WriteAsync(buffer.AsMemory(0, numRead), cancellationToken);
        }

        writer.Flush(true);
        return hasher.GetHashAndReset();
    }

    private static async Task<byte[]> HashFileAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var buffer = new byte[BufferSize];
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var reader = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );

        int numRead;
        while ((numRead = await reader.ReadAsync(buffer, cancellationToken)) != 0)
        {
            hasher.AppendData(buffer.AsSpan(0, numRead));
        }

        return hasher.GetHashAndReset();
    }

    private static PathSnapshot ReadSource(string sourcePath)
    {
        if (
            string.IsNullOrWhiteSpace(sourcePath)
            || !File.Exists(sourcePath) && !Directory.Exists(sourcePath)
        )
        {
            throw new FileNotFoundException("Source path is not found!", sourcePath);
        }

        return PathSnapshot.Capture(sourcePath);
    }

    private static bool HashesEqual(byte[] left, byte[] right) =>
        left.AsSpan().SequenceEqual(right);

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private enum AttemptResult
    {
        Published,
        SourceChanged,
        DestinationMismatch
    }
}
