using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TorrentExtractor.Settings
{
    public class Paths
    {
        public string Source { get; set; }
        public string[] WhitelistedWords { get; set; } = Array.Empty<string>();
        public string[] BlacklistedWords { get; set; } = Array.Empty<string>();
        public PathsByResolution Movies { get; set; } = new();
        public PathsByResolution TvShows { get; set; } = new();

        public void Validate()
        {
            if(string.IsNullOrWhiteSpace(Source) || Source.Length < 3)
                throw new ValidationException($"A valid {nameof(Source)} with at least 3 chars is required!");
            
            if(string.IsNullOrWhiteSpace(Movies?.ResDefault))
                throw new ValidationException($"A valid {nameof(Movies)}--{nameof(Movies.ResDefault)} is required!");
            
            if(string.IsNullOrWhiteSpace(TvShows?.ResDefault))
                throw new ValidationException($"A valid {nameof(TvShows)}--{nameof(TvShows.ResDefault)} is required!");
        }
        
        public override string ToString()
        {
            var info = new List<string> {$"{nameof(Source)}={Source}"};

            if(!string.IsNullOrWhiteSpace(Movies?.Res2160P))
                info.Add($"{nameof(Movies)}--{nameof(Movies.Res2160P)}={Movies.Res2160P}");
            
            if(!string.IsNullOrWhiteSpace(Movies?.Res1080P))
                info.Add($"{nameof(Movies)}--{nameof(Movies.Res1080P)}={Movies.Res1080P}");
            
            if(!string.IsNullOrWhiteSpace(Movies?.Res720P))
                info.Add($"{nameof(Movies)}--{nameof(Movies.Res720P)}={Movies.Res720P}");
            
            info.Add($"{nameof(Movies)}--{nameof(Movies.ResDefault)}={Movies?.ResDefault}");
            
            if(!string.IsNullOrWhiteSpace(TvShows?.Res2160P))
                info.Add($"{nameof(TvShows)}--{nameof(TvShows.Res2160P)}={TvShows.Res2160P}");
            
            if(!string.IsNullOrWhiteSpace(TvShows?.Res1080P))
                info.Add($"{nameof(TvShows)}--{nameof(TvShows.Res1080P)}={TvShows.Res1080P}");
            
            if(!string.IsNullOrWhiteSpace(TvShows?.Res720P))
                info.Add($"{nameof(TvShows)}--{nameof(TvShows.Res720P)}={TvShows.Res720P}");
            
            info.Add($"{nameof(TvShows)}--{nameof(TvShows.ResDefault)}={TvShows?.ResDefault}");
            
            return string.Join(", ", info);
        }

        public class PathsByResolution
        {
            public string Res2160P  { get; set; }
            public string Res1080P { get; set; }
            public string Res720P { get; set; }
            public string ResDefault { get; set; }
        }
    }
}