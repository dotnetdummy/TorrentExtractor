using TorrentExtractor.Settings;

namespace TorrentExtractor.Tests;

public class PathBuilderShould
{
    [Fact]
    public void GenerateSeasonPackTvPath()
    {
        var conf = new Paths
        {
            Source = "/src",
            Movies = new Paths.PathsByResolution { Default = "/movies" },
            Tv = new Paths.PathsByResolution { Default = "/tv" }
        };

        var srcPath = "/src/The.Test.Seasons.1-8.1080p.WEBRip.DD5.1.X.264-Testers";
        var actual = PathBuilder.GenerateDestinationPath(srcPath, conf);

        Assert.NotEmpty(actual);
        Assert.Equal("/tv/The Test", actual);
    }

    [Fact]
    public void GenerateSingleEpisodeTvPath()
    {
        var conf = new Paths
        {
            Source = "/src",
            Movies = new Paths.PathsByResolution { Default = "/movies" },
            Tv = new Paths.PathsByResolution { Default = "/tv" }
        };

        var srcPath =
            "/src/The.Test.S01E10.1080p.WEBRip.DD5.1.X.264-Testers/The.Test.S01E10.1080p.WEBRip.DD5.1.X.264-Testers.mkv";
        var actual = PathBuilder.GenerateDestinationPath(srcPath, conf);

        Assert.NotEmpty(actual);
        Assert.Equal("/tv/The Test/S01", actual);
    }

    [Fact]
    public void GenerateMoviesPath()
    {
        var conf = new Paths
        {
            Source = "/src",
            Movies = new Paths.PathsByResolution { Default = "/movies" },
            Tv = new Paths.PathsByResolution { Default = "/tv" }
        };

        var srcPath = "/src/Testing.2025.1080p.WEB.h264-Testers";
        var actual = PathBuilder.GenerateDestinationPath(srcPath, conf);

        Assert.NotEmpty(actual);
        Assert.Equal("/movies", actual);
    }
}
