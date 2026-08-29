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

    private static Paths MusicConf() =>
        new()
        {
            Source = "/src",
            Movies = new Paths.PathsByResolution { Default = "/movies" },
            Tv = new Paths.PathsByResolution { Default = "/tv" },
            Music = "/music"
        };
}
