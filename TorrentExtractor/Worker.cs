using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
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

            // Create a new FileSystemWatcher and set its properties.
            // ReSharper disable once UsingStatementResourceInitialization
            using var watcher = new FileSystemWatcher
            {
                Path = pathSettings.Source,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size
            };

            var processing = new ConcurrentBag<string>();

            // Add event handlers.
            watcher.Created += async (_, e) =>
            {
                if (processing.Contains(e.FullPath))
                    return;

                processing.Add(e.FullPath);

                await ProcessAsync(e.FullPath, coreSettings, pathSettings, cancellationToken);

                processing.TryTake(out var _);
            };

            // Begin watching.
            watcher.EnableRaisingEvents = true;

            // Watch until cancellation is requested
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not TaskCanceledException)
        {
            _logger.LogCritical(ex, "A critical error occurred");
            throw;
        }
    }

    private async Task AwaitFileCopy(
        string sourcePath,
        long previousFileLength,
        Core coreSettings,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            throw new TaskCanceledException();
        }

        var interval = coreSettings.FileCompareInterval;
        var fileLength = new FileInfo(sourcePath).Length;

        if (fileLength != previousFileLength)
        {
            _logger.LogInformation(
                "File '{SourcePath}' is still being copied. Waiting for {Interval} seconds...",
                sourcePath,
                interval
            );

            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);
            await AwaitFileCopy(sourcePath, fileLength, coreSettings, cancellationToken);
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
                pathSettings.WhitelistedWordsAsArray.Length != 0
                && !pathSettings.WhitelistedWordsAsArray.Any(word =>
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

            await AwaitFileCopy(sourcePath, -1, coreSettings, cancellationToken);

            await ExtractAndMoveAsync(
                sourcePath,
                GenerateDestinationPath(sourcePath, pathSettings)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred when processing '{SourcePath}'", sourcePath);
        }
    }

    private static string GenerateDestinationPath(string sourcePath, Paths paths)
    {
        var fileNameParts = Path.GetFileName(sourcePath)
            .Replace(" ", ".")
            .Split('.', StringSplitOptions.RemoveEmptyEntries);

        var validDestinationDir = false;
        var isTvShow = false;
        var tvShowSeason = string.Empty;
        var tvShowName = string.Empty;
        var destinationDir = string.Empty;
        var nameBuilder = new StringBuilder();

        foreach (var fileNamePart in fileNameParts)
        {
            var seasonPrefix = new[] { "S0", "S1", "S2", "S3", "S4", "S5" };
            if (
                seasonPrefix.Any(prefix =>
                    fileNamePart.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase)
                )
            )
            {
                isTvShow = true;
                tvShowSeason = fileNamePart
                    .Split('E', StringSplitOptions.RemoveEmptyEntries)[0]
                    .Split('e', StringSplitOptions.RemoveEmptyEntries)[0]
                    .Split("EP", StringSplitOptions.RemoveEmptyEntries)[0]
                    .Split("ep", StringSplitOptions.RemoveEmptyEntries)[0];
                tvShowName = nameBuilder.ToString();
            }

            switch (fileNamePart.ToUpper())
            {
                case "UHD":
                case "2160P":
                case "4K":
                    destinationDir = isTvShow
                        ? $"{(!string.IsNullOrWhiteSpace(paths.Tv.Res2160P) ? paths.Tv.Res2160P : paths.Tv.Default)}/{tvShowName}/{tvShowSeason}"
                        : !string.IsNullOrWhiteSpace(paths.Movies.Res2160P)
                            ? paths.Movies.Res2160P
                            : paths.Movies.Default;
                    validDestinationDir = true;
                    break;
                case "1080P":
                    destinationDir = isTvShow
                        ? $"{(!string.IsNullOrWhiteSpace(paths.Tv.Res1080P) ? paths.Tv.Res1080P : paths.Tv.Default)}/{tvShowName}/{tvShowSeason}"
                        : !string.IsNullOrWhiteSpace(paths.Movies.Res1080P)
                            ? paths.Movies.Res1080P
                            : paths.Movies.Default;
                    validDestinationDir = true;
                    break;
                case "720P":
                    destinationDir = isTvShow
                        ? $"{(!string.IsNullOrWhiteSpace(paths.Tv.Res720P) ? paths.Tv.Res720P : paths.Tv.Default)}/{tvShowName}/{tvShowSeason}"
                        : !string.IsNullOrWhiteSpace(paths.Movies.Res720P)
                            ? paths.Movies.Res720P
                            : paths.Movies.Default;
                    validDestinationDir = true;
                    break;
                default:
                    destinationDir = validDestinationDir
                        ? destinationDir
                        : isTvShow
                            ? $"{paths.Tv.Default}/{tvShowName}/{tvShowSeason}"
                            : paths.Movies.Default ?? paths.Movies.Default;
                    break;
            }

            nameBuilder.Append($"{(nameBuilder.Length == 0 ? "" : " ")}{fileNamePart}");
        }

        return destinationDir;
    }

    private async Task ExtractAndMoveAsync(string sourcePath, string destinationDir)
    {
        _logger.LogInformation("Ensuring directory exist '{DestinationDir}'", destinationDir);
        Directory.CreateDirectory(destinationDir);

        // Sometimes a "deletion" triggers an add just before the files are removed.. just double check again to get rid of exceptions
        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            _logger.LogInformation(
                "Oops! apparently it wasn't a new file being added, rather Transmission removing old ones. Skipping..."
            );
            return;
        }

        await ExtractAndMoveRecursionAsync(sourcePath, destinationDir);
    }

    private async Task ExtractAndMoveRecursionAsync(string sourcePath, string destinationDir)
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
                await ExtractAndMoveRecursionAsync(dir, destinationDir);
            }
            foreach (var file in Directory.GetFiles(sourcePath))
            {
                await ExtractAndMoveRecursionAsync(file, destinationDir);
            }

            return;
        }

        switch (Path.GetExtension(sourcePath))
        {
            case ".mkv":
            case ".avi":
            case ".mp4":
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
                File.Copy(sourcePath, destinationPath, true);
                _logger.LogInformation("Done copying file '{SourcePath}'", sourcePath);

                break;
            }

            case ".rar":
            {
                using var archive = RarArchive.Open(
                    sourcePath,
                    new ReaderOptions() { LeaveStreamOpen = true }
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
                var reader = ReaderFactory.Open(stream);

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
}
