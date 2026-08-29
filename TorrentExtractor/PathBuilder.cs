using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TorrentExtractor.Settings;

namespace TorrentExtractor;

public static class PathBuilder
{
    private static readonly string[] AudioMarkers =
    [
        "FLAC",
        "ALAC",
        "APE",
        "MP3",
        "M4A",
        "WAV",
        "OGG",
        "OPUS",
        "16BIT",
        "24BIT",
        "HI-RES",
        "HIRES"
    ];

    private static readonly string[] VideoMarkers =
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
        ".mp4"
    ];

    private static readonly string[] MetadataTokens =
    [
        "PROPER",
        "REPACK",
        "RETAIL",
        "WEB",
        "CD",
        "VINYL",
        "SAT",
        "LINE",
        "FLAC",
        "MP3",
        "AAC",
        "ALAC",
        "WAV",
        "OGG",
        "OPUS",
        "APE",
        "M4A",
        "16BIT",
        "24BIT",
        "16-BIT",
        "24-BIT",
        "HI-RES",
        "HIRES",
        "V0",
        "V2",
        "320",
        "320KBPS",
        "KBPS"
    ];

    public static bool IsMusic(string sourcePath)
    {
        var name = Path.GetFileName(sourcePath);
        return ContainsAny(name, AudioMarkers) && !ContainsAny(name, VideoMarkers);
    }

    public static string GenerateDestinationPath(string sourcePath, Paths paths)
    {
        if (IsMusic(sourcePath) && !string.IsNullOrWhiteSpace(paths.Music))
        {
            return GenerateMusicPath(sourcePath, paths);
        }

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

    private static string GenerateMusicPath(string sourcePath, Paths paths)
    {
        var musicRoot = paths.Music.TrimEnd('/');
        var releaseName = Path.GetFileName(sourcePath);

        if (TryParseArtistAlbum(releaseName, out var artist, out var album))
        {
            return $"{musicRoot}/{artist}/{album}";
        }

        return $"{musicRoot}/{releaseName}";
    }

    private static bool TryParseArtistAlbum(string releaseName, out string artist, out string album)
    {
        artist = string.Empty;
        album = string.Empty;

        var spacedDash = releaseName.IndexOf(" - ", StringComparison.Ordinal);
        if (spacedDash >= 0)
        {
            artist = releaseName[..spacedDash].Trim();
            album = StripAlbumMetadata(releaseName[(spacedDash + 3)..]);
            return !string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(album);
        }

        var tokens = releaseName.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            return false;
        }

        artist = Humanize(tokens[0]);
        var albumTokens = tokens
            .Skip(1)
            .TakeWhile(token => !IsMetadataToken(token))
            .Select(Humanize)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();

        album = string.Join(" ", albumTokens);
        return !string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(album);
    }

    private static string StripAlbumMetadata(string albumPart)
    {
        var result = Regex.Replace(albumPart, @"\[[^\]]*\]", " ");
        result = Regex.Replace(result, @"\((?:19|20)\d{2}\)", " ");
        result = Regex.Replace(result, @"\s*-\s*[A-Za-z0-9]+\s*$", "");

        var tokens = result.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (tokens.Count > 0 && IsMetadataToken(tokens[^1]))
        {
            tokens.RemoveAt(tokens.Count - 1);
        }

        return string.Join(" ", tokens);
    }

    private static bool IsMetadataToken(string token)
    {
        var normalized = token.Trim().TrimStart('[').TrimEnd(']');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^(?:19|20)\d{2}$"))
        {
            return true;
        }

        var upper = normalized.ToUpperInvariant();
        if (MetadataTokens.Contains(upper))
        {
            return true;
        }

        return Regex.IsMatch(upper, @"^CD\d+$") || Regex.IsMatch(upper, @"^\d+BIT$");
    }

    private static string Humanize(string token) =>
        string.Join(
            " ",
            token
                .Replace('_', ' ')
                .Replace('.', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        );

    private static bool ContainsAny(string value, string[] markers) =>
        markers.Any(marker => value.Contains(marker, StringComparison.InvariantCultureIgnoreCase));
}
