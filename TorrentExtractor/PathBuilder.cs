using System;
using System.IO;
using System.Linq;
using System.Text;
using TorrentExtractor.Settings;

namespace TorrentExtractor;

public static class PathBuilder
{
    public static string GenerateDestinationPath(string sourcePath, Paths paths)
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
            var seasonPrefix = new[] { "Season" }.Concat(
                new[] { "S0", "S1", "S2", "S3", "S4", "S5" }
            );
            if (
                seasonPrefix.Any(prefix =>
                    fileNamePart.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase)
                )
            )
            {
                isTvShow = true;
                tvShowSeason = fileNamePart
                    .Split("Seasons", StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault("")
                    .Split("Season", StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault("")
                    .Split('E', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault("")
                    .Split('e', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault("")
                    .Split("EP", StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault("")
                    .Split("ep", StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault("");
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

        return destinationDir.TrimEnd('/');
    }
}
