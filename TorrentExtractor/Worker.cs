using System;
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
    private readonly IOptions<General> _generalSettings;
    private readonly IOptions<Paths> _pathSettings;

    public Worker(ILogger<Worker> logger, IOptions<General> generalSettings, IOptions<Paths> pathSettings)
    {
        _logger = logger;
        _generalSettings = generalSettings;
        _pathSettings = pathSettings;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Application starting...");

            var generalSettings = _generalSettings.Value;
            var pathSettings = _pathSettings.Value;

            _logger.LogDebug("GeneralSettings: '{GeneralSettings}', PathSettings: '{Settings}'",
                generalSettings, pathSettings);
                
            generalSettings.Validate();
            pathSettings.Validate();

            // Create a new FileSystemWatcher and set its properties.
            using var watcher = new FileSystemWatcher
            {
                Path = pathSettings.Source
            };

            // Add event handlers.
            watcher.Created += (_, e) => OnChanged(e, generalSettings, pathSettings, cancellationToken);

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
        
    private void OnChanged(FileSystemEventArgs e, General generalSettings, Paths pathSettings, CancellationToken cancellationToken)
    {
        _logger.LogInformation("File/directory '{SourcePath}' {ChangeType}", e.FullPath, e.ChangeType);
        Task.Factory.StartNew(() => ProcessAsync(e.FullPath, generalSettings, pathSettings, cancellationToken), cancellationToken, TaskCreationOptions.AttachedToParent, TaskScheduler.Current);
    }

    private async Task ProcessAsync(string sourcePath, General generalSettings, Paths pathSettings, CancellationToken cancellationToken)
    {
        try
        {
            if (pathSettings.BlacklistedWords.Any())
            {
                foreach (var word in pathSettings.BlacklistedWords)
                {
                    if (!sourcePath.Contains(word, StringComparison.InvariantCultureIgnoreCase)) continue;
                    
                    _logger.LogInformation("A blacklisted word '{BlacklistedWord}' was found in the path '{FullPath}'. No further processing is done", word, sourcePath);
                    return;
                }
            }
                
            await ExtractAndMoveAsync(sourcePath, GenerateDestinationPath(sourcePath, pathSettings), generalSettings, cancellationToken);
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
            var seasonPrefix = new[] {"S0", "S1", "S2", "S3", "S4", "S5"};
            if (seasonPrefix.Any(prefix => fileNamePart.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                isTvShow = true;
                tvShowSeason = fileNamePart.Split('E', StringSplitOptions.RemoveEmptyEntries)[0]
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
                    destinationDir = isTvShow ? $"{(!string.IsNullOrWhiteSpace(paths.TvShows.Res2160P) ? paths.TvShows.Res2160P : paths.TvShows.ResDefault)}/{tvShowName}/{tvShowSeason}" : !string.IsNullOrWhiteSpace(paths.Movies.Res2160P) ? paths.Movies.Res2160P : paths.Movies.ResDefault;
                    validDestinationDir = true;
                    break;
                case "1080P":
                    destinationDir = isTvShow ? $"{(!string.IsNullOrWhiteSpace(paths.TvShows.Res1080P) ? paths.TvShows.Res1080P : paths.TvShows.ResDefault)}/{tvShowName}/{tvShowSeason}" : !string.IsNullOrWhiteSpace(paths.Movies.Res1080P) ? paths.Movies.Res1080P : paths.Movies.ResDefault;
                    validDestinationDir = true;
                    break;
                case "720P":
                    destinationDir = isTvShow ? $"{(!string.IsNullOrWhiteSpace(paths.TvShows.Res720P) ? paths.TvShows.Res720P : paths.TvShows.ResDefault)}/{tvShowName}/{tvShowSeason}" : !string.IsNullOrWhiteSpace(paths.Movies.Res720P) ? paths.Movies.Res720P : paths.Movies.ResDefault;
                    validDestinationDir = true;
                    break;
                default:
                    destinationDir = validDestinationDir ? destinationDir :
                        isTvShow ? $"{paths.TvShows.ResDefault}/{tvShowName}/{tvShowSeason}" : paths.Movies.ResDefault ?? paths.Movies.ResDefault;
                    break;
            }

            nameBuilder.Append($"{(nameBuilder.Length == 0 ? "" : " ")}{fileNamePart}");
        }
            
        return destinationDir;
    }

    private async Task ExtractAndMoveAsync(string sourcePath, string destinationDir, General generalSettings, CancellationToken cancellationToken)
    {
        var fileCopyDelaySeconds = generalSettings.FileCopyDelaySeconds;
            
        _logger.LogInformation("Ensuring directory exist '{DestinationDir}'", destinationDir);
        Directory.CreateDirectory(destinationDir);
            
        // Wait a while for all the files to be there...
        _logger.LogInformation("Awaiting file copy, continuing process in {FileCopyDelaySeconds} seconds", fileCopyDelaySeconds);
        await Task.Delay(fileCopyDelaySeconds * 1000, cancellationToken);

        // Sometimes a "deletion" through Transmission triggers an add just before the files are removed.. just double check again to get rid of exceptions
        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            _logger.LogInformation("Oops! apparently it wasn't a new file being added, rather Transmission removing old ones. Skipping...");
            return;
        }

        await ExtractAndMoveRecursionAsync(sourcePath, destinationDir);
    }

    private async Task ExtractAndMoveRecursionAsync(string sourcePath, string destinationDir)
    {
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
                var destinationPath = Path.Combine(destinationDir, filename);

                _logger.LogInformation("Copying file '{SourcePath}' to '{DestinationPath}'", sourcePath,
                    destinationPath);
                File.Copy(sourcePath, destinationPath, true);
                _logger.LogInformation("Done copying file '{SourcePath}'", sourcePath);

                break;
            }

            case ".rar":
            {
                using var archive = RarArchive.Open(sourcePath,
                    new ReaderOptions()
                    {
                        LeaveStreamOpen = true
                    });

                foreach (var entry in archive.Entries)
                {
                    if (!entry.IsDirectory)
                    {
                        _logger.LogInformation("Extracting file '{SourcePath}' to '{DestinationDir}'",
                            entry.Key, destinationDir);
                        entry.WriteToDirectory(destinationDir,
                            new ExtractionOptions()
                            {
                                ExtractFullPath = true,
                                Overwrite = true
                            });
                        _logger.LogInformation("Done extracting file '{SourcePath}'", entry.Key);
                    }
                    else
                    {
                        _logger.LogWarning("Extracting sub-dir is not supported! '{SubDirectory}'",
                            entry.Key);
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
                        _logger.LogInformation("Extracting '{SourcePath}' to '{DestinationDir}'",
                            reader.Entry.Key, destinationDir);
                        reader.WriteEntryToDirectory(destinationDir,
                            new ExtractionOptions() {ExtractFullPath = true, Overwrite = true});
                        _logger.LogInformation("Done extracting file '{SourceFile}'", reader.Entry.Key);
                    }
                    else
                    {
                        _logger.LogWarning("Extracting sub-dir is not supported! '{SubDirectory}'",
                            reader.Entry.Key);
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