using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Readers;
using TorrentExtractor.Settings;

namespace TorrentExtractor;

public class Worker : BackgroundService
{
    private static readonly string[] IncompleteSuffixes = [".!qb", ".part", ".!ut"];

    private readonly string[] _whitelistedWords =
    [
        "2160p",
        "1080p",
        "720p",
        "Webrip",
        "BluRay",
        "S0",
        "Season",
        "x264",
        "x265",
        "h264",
        "H.265",
        "h.264",
        "hevc",
        ".mkv",
        ".avi",
        ".mp4",
        "FLAC",
        "MP3",
        "ALAC",
        "APE",
        "WAV",
        "OGG",
        "OPUS",
        "16BIT",
        "24BIT",
        "HI-RES",
        "HIRES",
        ".flac",
        ".mp3",
        ".m4a"
    ];

    private readonly ILogger<Worker> _logger;
    private readonly IOptions<Core> _coreSettings;
    private readonly IOptions<Paths> _pathSettings;

    public Worker(ILogger<Worker> logger, IOptions<Core> coreSettings, IOptions<Paths> pathSettings)
    {
        _logger = logger;
        _coreSettings = coreSettings;
        _pathSettings = pathSettings;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Application starting...");

            var coreSettings = _coreSettings.Value;
            var pathSettings = _pathSettings.Value;

            _logger.LogDebug(
                "CoreSettings: '{CoreSettings}', PathSettings: '{Settings}'",
                coreSettings,
                pathSettings
            );

            coreSettings.Validate();
            pathSettings.Validate();

            await WaitForSourceAsync(pathSettings.Source, cancellationToken);

            using var processLock = new SemaphoreSlim(1, 1);
            // ReSharper disable once UsingStatementResourceInitialization
            using var watcher = new FileSystemWatcher { Path = pathSettings.Source };

            watcher.Created += (_, e) =>
                _ = HandleWatchEventAsync(
                    e.FullPath,
                    processLock,
                    coreSettings,
                    pathSettings,
                    cancellationToken
                );
            watcher.Renamed += (_, e) =>
                _ = HandleWatchEventAsync(
                    e.FullPath,
                    processLock,
                    coreSettings,
                    pathSettings,
                    cancellationToken
                );
            watcher.Error += (_, e) =>
                _logger.LogError(e.GetException(), "File system watcher error");

            watcher.EnableRaisingEvents = true;

            _logger.LogInformation("Watching directory '{SourcePath}'", pathSettings.Source);

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogCritical(ex, "A critical error occurred");
            Environment.Exit(1);
        }
    }

    private async Task WaitForSourceAsync(string source, CancellationToken cancellationToken)
    {
        while (!Directory.Exists(source))
        {
            _logger.LogWarning(
                "Source directory '{SourcePath}' does not exist yet. Waiting...",
                source
            );
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }

    private async Task HandleWatchEventAsync(
        string sourcePath,
        SemaphoreSlim processLock,
        Core coreSettings,
        Paths pathSettings,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await Task.Delay(1000, cancellationToken);

            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                return;
            }

            if (IsIncompletePath(sourcePath))
            {
                _logger.LogInformation("Skipping incomplete download '{FullPath}'", sourcePath);
                return;
            }

            await processLock.WaitAsync(cancellationToken);
            try
            {
                await ProcessAsync(sourcePath, coreSettings, pathSettings, cancellationToken);
            }
            finally
            {
                processLock.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "An error occurred when handling '{SourcePath}'", sourcePath);
        }
    }

    private async Task ProcessAsync(
        string sourcePath,
        Core coreSettings,
        Paths pathSettings,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (
                !_whitelistedWords.Any(word =>
                    sourcePath.Contains(word, StringComparison.InvariantCultureIgnoreCase)
                )
            )
            {
                _logger.LogInformation(
                    "No whitelisted word was found in the path '{FullPath}'. No further processing is done",
                    sourcePath
                );
                return;
            }

            if (
                pathSettings.BlacklistedWordsAsArray.Any(word =>
                    sourcePath.Contains(word, StringComparison.InvariantCultureIgnoreCase)
                )
            )
            {
                _logger.LogInformation(
                    "A blacklisted word was found in the path '{FullPath}'. No further processing is done",
                    sourcePath
                );
                return;
            }

            if (PathBuilder.IsMusic(sourcePath) && string.IsNullOrWhiteSpace(pathSettings.Music))
            {
                _logger.LogInformation(
                    "Music path is not configured. Skipping '{FullPath}'",
                    sourcePath
                );
                return;
            }

            if (!await AwaitFileCopy(sourcePath, coreSettings, cancellationToken))
            {
                return;
            }

            await ExtractAndMoveAsync(
                sourcePath,
                PathBuilder.GenerateDestinationPath(sourcePath, pathSettings),
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred when processing '{SourcePath}'", sourcePath);
        }
    }

    private async Task<bool> AwaitFileCopy(
        string sourcePath,
        Core coreSettings,
        CancellationToken cancellationToken
    )
    {
        var interval = TimeSpan.FromSeconds(coreSettings.FileCompareInterval);
        var deadline = Stopwatch.StartNew();
        var maxWait = TimeSpan.FromHours(coreSettings.MaxSettleHours);
        long previousLength = -1;

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (
                string.IsNullOrWhiteSpace(sourcePath)
                || !File.Exists(sourcePath) && !Directory.Exists(sourcePath)
            )
            {
                throw new FileNotFoundException("Source path is not found!", sourcePath);
            }

            var length = Directory.Exists(sourcePath)
                ? new DirectoryInfo(sourcePath).Length()
                : new FileInfo(sourcePath).Length;

            if (length == previousLength)
            {
                return true;
            }

            if (deadline.Elapsed >= maxWait)
            {
                _logger.LogError(
                    "File '{SourcePath}' did not settle within {MaxSettleHours} hours. Skipping",
                    sourcePath,
                    coreSettings.MaxSettleHours
                );
                return false;
            }

            _logger.LogInformation(
                "File '{SourcePath}' is still being copied. Waiting for {Interval} seconds...",
                sourcePath,
                coreSettings.FileCompareInterval
            );

            await Task.Delay(interval, cancellationToken);
            previousLength = length;
        }
    }

    private async Task ExtractAndMoveAsync(
        string sourcePath,
        string destinationDir,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation("Ensuring directory exist '{DestinationDir}'", destinationDir);
        Directory.CreateDirectory(destinationDir);
        await ExtractAndMoveRecursionAsync(sourcePath, destinationDir, cancellationToken);
    }

    private async Task ExtractAndMoveRecursionAsync(
        string sourcePath,
        string destinationDir,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            _logger.LogError("Source path is empty!");
            return;
        }

        if (Directory.Exists(sourcePath))
        {
            foreach (var dir in Directory.GetDirectories(sourcePath))
            {
                await ExtractAndMoveRecursionAsync(dir, destinationDir, cancellationToken);
            }
            foreach (var file in Directory.GetFiles(sourcePath))
            {
                await ExtractAndMoveRecursionAsync(file, destinationDir, cancellationToken);
            }

            return;
        }

        switch (Path.GetExtension(sourcePath).ToLowerInvariant())
        {
            case ".mkv":
            case ".avi":
            case ".mp4":
            case ".flac":
            case ".mp3":
            case ".m4a":
            case ".aac":
            case ".wav":
            case ".ogg":
            case ".opus":
            case ".wma":
            case ".ape":
            case ".aiff":
            case ".aif":
            case ".wv":
            {
                var filename = Path.GetFileName(sourcePath);

                if (string.IsNullOrWhiteSpace(filename))
                {
                    _logger.LogError("Filename is empty!");
                    return;
                }

                var destinationPath = Path.Combine(destinationDir, filename);

                _logger.LogInformation(
                    "Copying file '{SourcePath}' to '{DestinationPath}'",
                    sourcePath,
                    destinationPath
                );

                await CopyFileAsync(sourcePath, destinationPath, cancellationToken);

                var sourceLength = Directory.Exists(sourcePath)
                    ? new DirectoryInfo(sourcePath).Length()
                    : new FileInfo(sourcePath).Length;

                var destinationLength = Directory.Exists(destinationPath)
                    ? new DirectoryInfo(destinationPath).Length()
                    : new FileInfo(destinationPath).Length;

                if (sourceLength != destinationLength)
                {
                    _logger.LogInformation(
                        "File length do not match! '{SourcePath}' is {SourceLength} and '{DestinationPath}' is {DestinationLength}. Retrying...",
                        sourcePath,
                        sourceLength,
                        destinationPath,
                        destinationLength
                    );

                    await CopyFileAsync(sourcePath, destinationPath, cancellationToken);

                    sourceLength = Directory.Exists(sourcePath)
                        ? new DirectoryInfo(sourcePath).Length()
                        : new FileInfo(sourcePath).Length;

                    destinationLength = Directory.Exists(destinationPath)
                        ? new DirectoryInfo(destinationPath).Length()
                        : new FileInfo(destinationPath).Length;

                    if (sourceLength != destinationLength)
                    {
                        _logger.LogError(
                            "An error occurred when copying '{SourcePath}' to '{DestinationPath}'. Cleaning up corrupted files...",
                            sourcePath,
                            destinationPath
                        );

                        if (Directory.Exists(destinationPath))
                        {
                            Directory.Delete(destinationPath, true);
                        }
                        else
                        {
                            File.Delete(destinationPath);
                        }

                        break;
                    }
                }

                _logger.LogInformation("Done copying file '{SourcePath}'", sourcePath);

                break;
            }

            case ".rar":
            {
                using var archive = RarArchive.OpenArchive(
                    sourcePath,
                    new ReaderOptions() { LeaveStreamOpen = false }
                );

                foreach (var entry in archive.Entries)
                {
                    if (!entry.IsDirectory)
                    {
                        _logger.LogInformation(
                            "Extracting file '{SourcePath}' to '{DestinationDir}'",
                            entry.Key,
                            destinationDir
                        );
                        entry.WriteToDirectory(
                            destinationDir,
                            new ExtractionOptions() { ExtractFullPath = true, Overwrite = true }
                        );
                        _logger.LogInformation("Done extracting file '{SourcePath}'", entry.Key);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Extracting sub-dir is not supported! '{SubDirectory}'",
                            entry.Key
                        );
                    }
                }

                break;
            }

            case ".zip":
            {
                await using var stream = File.OpenRead(sourcePath);
                var reader = ReaderFactory.OpenReader(stream);

                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        _logger.LogInformation(
                            "Extracting '{SourcePath}' to '{DestinationDir}'",
                            reader.Entry.Key,
                            destinationDir
                        );
                        reader.WriteEntryToDirectory(
                            destinationDir,
                            new ExtractionOptions() { ExtractFullPath = true, Overwrite = true }
                        );
                        _logger.LogInformation(
                            "Done extracting file '{SourceFile}'",
                            reader.Entry.Key
                        );
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Extracting sub-dir is not supported! '{SubDirectory}'",
                            reader.Entry.Key
                        );
                    }
                }

                break;
            }
            default:
            {
                _logger.LogDebug("File not supported '{SourcePath}'", sourcePath);
                break;
            }
        }
    }

    private static bool IsIncompletePath(string sourcePath)
    {
        var name = Path.GetFileName(sourcePath);
        return IncompleteSuffixes.Any(suffix =>
            name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static async Task CopyFileAsync(
        string src,
        string dest,
        CancellationToken cancellationToken
    )
    {
        var buffer = new byte[1024 * 1024];
        int numRead;

        await using var reader = File.Open(
            src,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite
        );
        await using var writer = new FileStream(
            dest,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            buffer.Length,
            FileOptions.Asynchronous
        );
        while ((numRead = await reader.ReadAsync(buffer, cancellationToken)) != 0)
        {
            await writer.WriteAsync(buffer.AsMemory(0, numRead), cancellationToken);
        }
    }
}
