using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TorrentExtractor.Settings;

namespace TorrentExtractor;

public static class PathBuilder
{
    public static readonly string[] AudioExtensions =
    [
        ".flac",
        ".mp3",
        ".m4a",
        ".aac",
        ".wav",
        ".ogg",
        ".opus",
        ".wma",
        ".ape",
        ".aiff",
        ".aif",
        ".wv"
    ];

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

    private static readonly Regex SceneSeasonRegex =
        new(
            @"^S(\d{2})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
        );

    private static readonly Regex SeasonNumberRegex = new(@"^\d{1,2}$", RegexOptions.Compiled);

    private static readonly Regex SeasonRangeRegex =
        new(@"^\d{1,2}-\d{1,2}$", RegexOptions.Compiled);

    public static bool IsAudioFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var ext = Path.GetExtension(path);
        return AudioExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsMusic(string sourcePath, IEnumerable<string> containedFilePaths = null)
    {
        if (containedFilePaths != null && containedFilePaths.Any(IsAudioFile))
        {
            return true;
        }

        if (IsAudioFile(sourcePath))
        {
            return true;
        }

        var name = Path.GetFileName(sourcePath);
        return ContainsAny(name, AudioMarkers) && !ContainsAny(name, VideoMarkers);
    }

    public static string GenerateDestinationPath(
        string sourcePath,
        Paths paths,
        IEnumerable<string> containedFilePaths = null
    )
    {
        if (IsMusic(sourcePath, containedFilePaths) && !string.IsNullOrWhiteSpace(paths.Music))
        {
            return GenerateMusicPath(sourcePath, paths);
        }

        var fileNameParts = Path.GetFileName(sourcePath)
            .Replace(" ", ".")
            .Split('.', StringSplitOptions.RemoveEmptyEntries);

        var isTvShow = false;
        var tvShowSeason = string.Empty;
        var tvShowName = string.Empty;
        string resolution = null;

        for (var i = 0; i < fileNameParts.Length; i++)
        {
            var token = StripBrackets(fileNameParts[i]);
            var nextToken =
                i + 1 < fileNameParts.Length ? StripBrackets(fileNameParts[i + 1]) : string.Empty;

            if (!isTvShow && TryReadTvSeason(token, nextToken, out var season))
            {
                isTvShow = true;
                tvShowSeason = season;
                tvShowName = string.Join(
                    " ",
                    fileNameParts
                        .Take(i)
                        .Where(part => ParseResolution(StripBrackets(part)) is null)
                );
            }

            resolution ??= ParseResolution(token);
        }

        return BuildVideoDestination(paths, isTvShow, tvShowName, tvShowSeason, resolution);
    }

    private static string BuildVideoDestination(
        Paths paths,
        bool isTvShow,
        string tvShowName,
        string tvShowSeason,
        string resolution
    )
    {
        if (isTvShow)
        {
            var tvRoot = resolution switch
            {
                "2160" => FirstNonEmpty(paths.Tv.Res2160P, paths.Tv.Default),
                "1080" => FirstNonEmpty(paths.Tv.Res1080P, paths.Tv.Default),
                "720" => FirstNonEmpty(paths.Tv.Res720P, paths.Tv.Default),
                _ => paths.Tv.Default
            };

            return $"{tvRoot}/{tvShowName}/{tvShowSeason}".TrimEnd('/');
        }

        return resolution switch
        {
            "2160" => FirstNonEmpty(paths.Movies.Res2160P, paths.Movies.Default),
            "1080" => FirstNonEmpty(paths.Movies.Res1080P, paths.Movies.Default),
            "720" => FirstNonEmpty(paths.Movies.Res720P, paths.Movies.Default),
            _ => paths.Movies.Default
        };
    }

    private static bool TryReadTvSeason(string token, string nextToken, out string season)
    {
        season = string.Empty;

        var sceneMatch = SceneSeasonRegex.Match(token);
        if (sceneMatch.Success)
        {
            season = $"S{sceneMatch.Groups[1].Value}";
            return true;
        }

        if (
            !token.Equals("Season", StringComparison.OrdinalIgnoreCase)
            && !token.Equals("Seasons", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        if (SeasonRangeRegex.IsMatch(nextToken))
        {
            season = string.Empty;
            return true;
        }

        if (SeasonNumberRegex.IsMatch(nextToken))
        {
            season = $"S{int.Parse(nextToken):D2}";
            return true;
        }

        return false;
    }

    private static string ParseResolution(string token) =>
        token.ToUpperInvariant() switch
        {
            "UHD" or "2160P" or "4K" => "2160",
            "1080P" => "1080",
            "720P" => "720",
            _ => null
        };

    private static string StripBrackets(string token) => token.Trim().Trim('[', ']', '(', ')');

    private static string FirstNonEmpty(string preferred, string fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;

    public static string GenerateMusicFileDestination(
        string sourceRoot,
        string filePath,
        Paths paths
    )
    {
        var root = sourceRoot.TrimEnd('/', '\\');
        var relative = Path.GetRelativePath(root, filePath);
        var parentRel = Path.GetDirectoryName(relative);

        if (string.IsNullOrEmpty(parentRel) || parentRel == ".")
        {
            return GenerateMusicPathFromReleaseName(Path.GetFileName(filePath), paths);
        }

        var segments = parentRel.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 1)
        {
            return GenerateMusicPathFromReleaseName(segments[0], paths);
        }

        var musicRoot = paths.Music.TrimEnd('/');
        return $"{musicRoot}/{segments[^2]}/{segments[^1]}";
    }

    private static string GenerateMusicPath(string sourcePath, Paths paths) =>
        GenerateMusicPathFromReleaseName(Path.GetFileName(sourcePath), paths);

    private static string GenerateMusicPathFromReleaseName(string releaseName, Paths paths)
    {
        var musicRoot = paths.Music.TrimEnd('/');

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
