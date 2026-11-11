using System;
using System.Collections.Generic;
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
            var nameWhitelisted = _whitelistedWords.Any(word =>
                sourcePath.Contains(word, StringComparison.InvariantCultureIgnoreCase)
            );

            if (
                !nameWhitelisted
                && !Directory.Exists(sourcePath)
                && !PathBuilder.IsAudioFile(sourcePath)
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

            if (!await AwaitFileCopy(sourcePath, coreSettings, cancellationToken))
            {
                return;
            }

            var containedFiles = ListContainedFiles(sourcePath);
            var isMusic = PathBuilder.IsMusic(sourcePath, containedFiles);

            if (isMusic && string.IsNullOrWhiteSpace(pathSettings.Music))
            {
                _logger.LogInformation(
                    "Music path is not configured. Skipping '{FullPath}'",
                    sourcePath
                );
                return;
            }

            if (!isMusic && !nameWhitelisted)
            {
                _logger.LogInformation(
                    "No audio files or whitelisted word was found in '{FullPath}'. No further processing is done",
                    sourcePath
                );
                return;
            }

            await ExtractAndMoveAsync(
                sourcePath,
                pathSettings,
                isMusic,
                containedFiles,
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
        var settled = await DownloadSettle.WaitAsync(
            () =>
            {
                if (
                    string.IsNullOrWhiteSpace(sourcePath)
                    || !File.Exists(sourcePath) && !Directory.Exists(sourcePath)
                )
                {
                    throw new FileNotFoundException("Source path is not found!", sourcePath);
                }

                return PathSnapshot.Capture(sourcePath);
            },
            TimeSpan.FromSeconds(coreSettings.FileCompareInterval),
            TimeSpan.FromHours(coreSettings.MaxSettleHours),
            Task.Delay,
            () =>
                _logger.LogInformation(
                    "File '{SourcePath}' is still being copied. Waiting for {Interval} seconds...",
                    sourcePath,
                    coreSettings.FileCompareInterval
                ),
            cancellationToken
        );

        if (!settled)
        {
            _logger.LogError(
                "File '{SourcePath}' did not settle within {MaxSettleHours} hours. Skipping",
                sourcePath,
                coreSettings.MaxSettleHours
            );
            return false;
        }

        return true;
    }

    private static string[] ListContainedFiles(string sourcePath)
    {
        if (Directory.Exists(sourcePath))
        {
            return Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
        }

        if (File.Exists(sourcePath))
        {
            return [sourcePath];
        }

        return [];
    }

    private async Task ExtractAndMoveAsync(
        string sourcePath,
        Paths pathSettings,
        bool isMusic,
        IEnumerable<string> containedFiles,
        CancellationToken cancellationToken
    )
    {
        string destinationDir = null;
        if (!isMusic)
        {
            destinationDir = PathBuilder.GenerateDestinationPath(
                sourcePath,
                pathSettings,
                containedFiles
            );
            _logger.LogInformation("Ensuring directory exist '{DestinationDir}'", destinationDir);
            Directory.CreateDirectory(destinationDir);
        }

        await ExtractAndMoveRecursionAsync(
            sourcePath,
            destinationDir,
            isMusic,
            pathSettings,
            cancellationToken
        );
    }

    private string ResolveDestinationDir(
        string filePath,
        string destinationDir,
        bool isMusic,
        Paths pathSettings
    )
    {
        if (!isMusic)
        {
            return destinationDir;
        }

        var dest = PathBuilder.GenerateMusicFileDestination(
            pathSettings.Source,
            filePath,
            pathSettings
        );
        _logger.LogInformation("Ensuring directory exist '{DestinationDir}'", dest);
        Directory.CreateDirectory(dest);
        return dest;
    }

    private async Task ExtractAndMoveRecursionAsync(
        string sourcePath,
        string destinationDir,
        bool isMusic,
        Paths pathSettings,
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
            if (PathBuilder.IsSamplePath(sourcePath))
            {
                _logger.LogInformation("Skipping sample '{SourcePath}'", sourcePath);
                return;
            }

            foreach (var dir in Directory.GetDirectories(sourcePath))
            {
                await ExtractAndMoveRecursionAsync(
                    dir,
                    destinationDir,
                    isMusic,
                    pathSettings,
                    cancellationToken
                );
            }
            foreach (var file in Directory.GetFiles(sourcePath))
            {
                await ExtractAndMoveRecursionAsync(
                    file,
                    destinationDir,
                    isMusic,
                    pathSettings,
                    cancellationToken
                );
            }

            return;
        }

        if (PathBuilder.IsSamplePath(sourcePath))
        {
            _logger.LogInformation("Skipping sample '{SourcePath}'", sourcePath);
            return;
        }

        switch (Path.GetExtension(sourcePath).ToLowerInvariant())
        {
            case ".mkv":
            case ".avi":
            case ".mp4":
            case { } when PathBuilder.IsAudioFile(sourcePath):
            {
                var filename = Path.GetFileName(sourcePath);

                if (string.IsNullOrWhiteSpace(filename))
                {
                    _logger.LogError("Filename is empty!");
                    return;
                }

                destinationDir = ResolveDestinationDir(
                    sourcePath,
                    destinationDir,
                    isMusic,
                    pathSettings
                );
                var destinationPath = Path.Combine(destinationDir, filename);

                _logger.LogInformation(
                    "Copying file '{SourcePath}' to '{DestinationPath}'",
                    sourcePath,
                    destinationPath
                );

                var core = _coreSettings.Value;
                var copied = await VerifiedFileCopy.CopyAsync(
                    sourcePath,
                    destinationPath,
                    TimeSpan.FromSeconds(core.FileCompareInterval),
                    TimeSpan.FromHours(core.MaxSettleHours),
                    _logger,
                    cancellationToken
                );

                if (!copied)
                {
                    break;
                }

                _logger.LogInformation("Done copying file '{SourcePath}'", sourcePath);

                break;
            }

            case ".rar":
            {
                destinationDir = ResolveDestinationDir(
                    sourcePath,
                    destinationDir,
                    isMusic,
                    pathSettings
                );
                using var archive = RarArchive.OpenArchive(
                    sourcePath,
                    new ReaderOptions() { LeaveStreamOpen = false }
                );

                foreach (var entry in archive.Entries)
                {
                    if (!entry.IsDirectory)
                    {
                        if (PathBuilder.IsSamplePath(entry.Key))
                        {
                            _logger.LogInformation("Skipping sample '{SourcePath}'", entry.Key);
                            continue;
                        }

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
                destinationDir = ResolveDestinationDir(
                    sourcePath,
                    destinationDir,
                    isMusic,
                    pathSettings
                );
                await using var stream = File.OpenRead(sourcePath);
                var reader = ReaderFactory.OpenReader(stream);

                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        if (PathBuilder.IsSamplePath(reader.Entry.Key))
                        {
                            _logger.LogInformation(
                                "Skipping sample '{SourcePath}'",
                                reader.Entry.Key
                            );
                            continue;
                        }

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
}
