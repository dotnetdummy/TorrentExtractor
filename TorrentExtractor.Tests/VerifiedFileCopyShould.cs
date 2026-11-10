using Microsoft.Extensions.Logging.Abstractions;

namespace TorrentExtractor.Tests;

public class VerifiedFileCopyShould
{
    [Fact]
    public async Task Publish_when_the_copy_matches_the_source()
    {
        using var dir = new TempDirectory();
        var source = Path.Combine(dir.Path, "source.mkv");
        var destination = Path.Combine(dir.Path, "library", "movie.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var data = new byte[(1024 * 1024) + 64];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 251);
        }

        File.WriteAllBytes(source, data);

        var published = await VerifiedFileCopy.CopyAsync(
            source,
            destination,
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromSeconds(2),
            NullLogger.Instance,
            CancellationToken.None
        );

        Assert.True(published);
        Assert.Equal(data, File.ReadAllBytes(destination));
        Assert.False(File.Exists(destination + ".partial"));
    }

    [Fact]
    public async Task Retry_then_publish_when_the_first_partial_does_not_match()
    {
        using var dir = new TempDirectory();
        var source = Path.Combine(dir.Path, "source.mkv");
        var destination = Path.Combine(dir.Path, "movie.mkv");
        var data = new byte[] { 1, 2, 3, 4 };
        File.WriteAllBytes(source, data);
        var attempts = 0;

        var published = await VerifiedFileCopy.CopyAsync(
            source,
            destination,
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromSeconds(2),
            NullLogger.Instance,
            CancellationToken.None,
            (_, _) =>
            {
                attempts++;
                if (attempts == 1)
                {
                    File.WriteAllBytes(destination + ".partial", [9]);
                }

                return Task.CompletedTask;
            }
        );

        Assert.True(published);
        Assert.Equal(2, attempts);
        Assert.Equal(data, File.ReadAllBytes(destination));
        Assert.False(File.Exists(destination + ".partial"));
    }

    [Fact]
    public async Task Leave_the_final_name_unpublished_when_the_partial_never_matches()
    {
        using var dir = new TempDirectory();
        var source = Path.Combine(dir.Path, "source.mkv");
        var destination = Path.Combine(dir.Path, "movie.mkv");
        File.WriteAllBytes(source, [1, 2, 3, 4]);
        var attempts = 0;

        var published = await VerifiedFileCopy.CopyAsync(
            source,
            destination,
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromSeconds(2),
            NullLogger.Instance,
            CancellationToken.None,
            (partial, _) =>
            {
                attempts++;
                Assert.False(File.Exists(destination));
                Assert.True(File.Exists(partial));
                File.WriteAllBytes(partial, [9, 9, 9]);
                return Task.CompletedTask;
            }
        );

        Assert.False(published);
        Assert.Equal(3, attempts);
        Assert.False(File.Exists(destination));
        Assert.False(File.Exists(destination + ".partial"));
    }

    [Fact]
    public async Task Settle_again_instead_of_publishing_when_the_source_changes()
    {
        using var dir = new TempDirectory();
        var source = Path.Combine(dir.Path, "source.mkv");
        var destination = Path.Combine(dir.Path, "movie.mkv");
        File.WriteAllBytes(source, [1, 2, 3, 4]);
        var attempts = 0;

        var published = await VerifiedFileCopy.CopyAsync(
            source,
            destination,
            TimeSpan.FromMilliseconds(30),
            TimeSpan.FromMilliseconds(250),
            NullLogger.Instance,
            CancellationToken.None,
            (_, _) =>
            {
                attempts++;
                File.WriteAllBytes(source, [(byte)(attempts + 1), 2, 3, 4]);
                return Task.CompletedTask;
            }
        );

        Assert.False(published);
        Assert.True(attempts >= 1);
        Assert.False(File.Exists(destination));
        Assert.False(File.Exists(destination + ".partial"));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, true);
    }
}
