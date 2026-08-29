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

    [Fact]
    public void GenerateMusicPathFromSpacedDashRelease()
    {
        var conf = MusicConf();
        var srcPath =
            "/src/Drum Diggers - Your Absolute Unit [2023] [Hi-Res] [FLAC-24Bit]-OldSchool";
        var actual = PathBuilder.GenerateDestinationPath(srcPath, conf);

        Assert.Equal("/music/Drum Diggers/Your Absolute Unit", actual);
    }

    [Fact]
    public void GenerateMusicPathFromSceneHyphenRelease()
    {
        var conf = MusicConf();
        var srcPath = "/src/Tramderz-Chaos.Monkeyz-2005-WEB-FLAC-16bit-W00TM8";
        var actual = PathBuilder.GenerateDestinationPath(srcPath, conf);

        Assert.Equal("/music/Tramderz/Chaos Monkeyz", actual);
    }

    [Fact]
    public void GenerateMusicPathFromUnderscoreSceneRelease()
    {
        var conf = MusicConf();
        var srcPath = "/src/Holy_Cow_A_Plaster-Memories-PROPER-CD-FLAC-2002-TTR";
        var actual = PathBuilder.GenerateDestinationPath(srcPath, conf);

        Assert.Equal("/music/Holy Cow A Plaster/Memories", actual);
    }

    [Fact]
    public void GenerateMusicPathFallsBackToFolderName()
    {
        var conf = MusicConf();
        var srcPath = "/src/SomeAlbum_FLAC";
        var actual = PathBuilder.GenerateDestinationPath(srcPath, conf);

        Assert.Equal("/music/SomeAlbum_FLAC", actual);
    }

    [Fact]
    public void GenerateMoviesPathWhenReleaseIncludesAac()
    {
        var conf = MusicConf();
        var srcPath = "/src/Testing.2025.1080p.WEB.AAC-Testers";
        var actual = PathBuilder.GenerateDestinationPath(srcPath, conf);

        Assert.Equal("/movies", actual);
    }

    [Fact]
    public void GenerateMoviesPathWhenTitleContainsSeason()
    {
        var conf = VideoConf();
        var srcPath = "/src/Season.of.the.Witch.2011.1080p.BluRay.x264-Testers";
        var actual = PathBuilder.GenerateDestinationPath(srcPath, conf);

        Assert.Equal("/movies", actual);
    }

    [Fact]
    public void GenerateTvPathWhenResolutionAppearsBeforeSeason()
    {
        var conf = VideoConf(tv1080: "/tv1080");
        var srcPath = "/src/The.Show.1080p.S01E01.WEBRip.x264-Testers";
        var actual = PathBuilder.GenerateDestinationPath(srcPath, conf);

        Assert.Equal("/tv1080/The Show/S01", actual);
    }

    [Fact]
    public void GenerateMoviesPathFromYtsBracketedResolution()
    {
        var conf = VideoConf(movies1080: "/movies1080");
        var srcPath = "/src/Movie.Title.2019.[1080p].[YTS.MX]";
        var actual = PathBuilder.GenerateDestinationPath(srcPath, conf);

        Assert.Equal("/movies1080", actual);
    }

    [Fact]
    public void GenerateMoviesPathFromSpacedYtsRelease()
    {
        var conf = VideoConf(movies1080: "/movies1080");
        var srcPath = "/src/Movie Title (2019) [1080p] [YTS.MX]";
        var actual = PathBuilder.GenerateDestinationPath(srcPath, conf);

        Assert.Equal("/movies1080", actual);
    }

    [Fact]
    public void GenerateTvPathForSeasonSixty()
    {
        var conf = VideoConf();
        var srcPath = "/src/The.Test.S60E01.1080p.WEBRip.x264-Testers";
        var actual = PathBuilder.GenerateDestinationPath(srcPath, conf);

        Assert.Equal("/tv/The Test/S60", actual);
    }

    [Theory]
    [InlineData("UHD", "/movies2160")]
    [InlineData("4K", "/movies2160")]
    [InlineData("2160p", "/movies2160")]
    [InlineData("1080p", "/movies1080")]
    [InlineData("720p", "/movies720")]
    public void GenerateMoviesPathByResolutionToken(string resolution, string expected)
    {
        var conf = VideoConf(
            movies2160: "/movies2160",
            movies1080: "/movies1080",
            movies720: "/movies720"
        );
        var srcPath = $"/src/Testing.2025.{resolution}.WEB.h264-Testers";
        var actual = PathBuilder.GenerateDestinationPath(srcPath, conf);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GenerateTvPathByResolutionToken()
    {
        var conf = VideoConf(tv2160: "/tv2160", tv720: "/tv720");
        var uhd = PathBuilder.GenerateDestinationPath(
            "/src/The.Test.S01E01.UHD.BluRay.x265-Testers",
            conf
        );
        var p720 = PathBuilder.GenerateDestinationPath(
            "/src/The.Test.S01E01.720p.WEBRip.x264-Testers",
            conf
        );

        Assert.Equal("/tv2160/The Test/S01", uhd);
        Assert.Equal("/tv720/The Test/S01", p720);
    }

    private static Paths VideoConf(
        string? movies2160 = null,
        string? movies1080 = null,
        string? movies720 = null,
        string? tv2160 = null,
        string? tv1080 = null,
        string? tv720 = null
    ) =>
        new()
        {
            Source = "/src",
            Movies = new Paths.PathsByResolution
            {
                Default = "/movies",
                Res2160P = movies2160,
                Res1080P = movies1080,
                Res720P = movies720
            },
            Tv = new Paths.PathsByResolution
            {
                Default = "/tv",
                Res2160P = tv2160,
                Res1080P = tv1080,
                Res720P = tv720
            }
        };

    private static Paths MusicConf() =>
        new()
        {
            Source = "/src",
            Movies = new Paths.PathsByResolution { Default = "/movies" },
            Tv = new Paths.PathsByResolution { Default = "/tv" },
            Music = "/music"
        };
}
