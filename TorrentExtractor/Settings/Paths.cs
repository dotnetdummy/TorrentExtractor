using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace TorrentExtractor.Settings
{
    public class Paths
    {
        /// <summary>
        /// (required) Source directory to watch for new files.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Comma separated list of words to blacklist files in source directory. If not set, all files will be processed.
        /// </summary>
        public string BlacklistedWords { get; set; }

        /// <summary>
        /// Array of words to blacklist files in source directory. If empty, all files will be processed.
        /// </summary>
        public string[] BlacklistedWordsAsArray =>
            BlacklistedWords?.Split(',').ToArray() ?? Array.Empty<string>();

        /// <summary>
        /// Contains the paths for movies by resolution.
        /// </summary>
        public PathsByResolution Movies { get; set; } = new();

        /// <summary>
        /// Contains the paths for tv shows by resolution.
        /// </summary>
        public PathsByResolution Tv { get; set; } = new();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Source) || Source.Length < 3)
                throw new ValidationException(
                    $"A valid {nameof(Source)} with at least 3 chars is required!"
                );

            if (string.IsNullOrWhiteSpace(Movies?.Default))
                throw new ValidationException(
                    $"A valid {nameof(Movies)}__{nameof(Movies.Default)} is required!"
                );

            if (string.IsNullOrWhiteSpace(Tv?.Default))
                throw new ValidationException(
                    $"A valid {nameof(Tv)}__{nameof(Tv.Default)} is required!"
                );
        }

        public override string ToString()
        {
            var info = new List<string> { $"{nameof(Source)}={Source}" };

            if (!string.IsNullOrWhiteSpace(Movies?.Res2160P))
                info.Add($"{nameof(Movies)}__{nameof(Movies.Res2160P)}={Movies.Res2160P}");

            if (!string.IsNullOrWhiteSpace(Movies?.Res1080P))
                info.Add($"{nameof(Movies)}__{nameof(Movies.Res1080P)}={Movies.Res1080P}");

            if (!string.IsNullOrWhiteSpace(Movies?.Res720P))
                info.Add($"{nameof(Movies)}__{nameof(Movies.Res720P)}={Movies.Res720P}");

            info.Add($"{nameof(Movies)}__{nameof(Movies.Default)}={Movies?.Default}");

            if (!string.IsNullOrWhiteSpace(Tv?.Res2160P))
                info.Add($"{nameof(Tv)}__{nameof(Tv.Res2160P)}={Tv.Res2160P}");

            if (!string.IsNullOrWhiteSpace(Tv?.Res1080P))
                info.Add($"{nameof(Tv)}__{nameof(Tv.Res1080P)}={Tv.Res1080P}");

            if (!string.IsNullOrWhiteSpace(Tv?.Res720P))
                info.Add($"{nameof(Tv)}__{nameof(Tv.Res720P)}={Tv.Res720P}");

            info.Add($"{nameof(Tv)}__{nameof(Tv.Default)}={Tv?.Default}");

            return string.Join(", ", info);
        }

        public class PathsByResolution
        {
            [ConfigurationKeyName("2160P")]
            public string Res2160P { get; set; }

            [ConfigurationKeyName("1080P")]
            public string Res1080P { get; set; }

            [ConfigurationKeyName("720P")]
            public string Res720P { get; set; }
            public string Default { get; set; }
        }
    }
}
