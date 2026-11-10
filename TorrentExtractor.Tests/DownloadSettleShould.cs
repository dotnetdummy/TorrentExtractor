namespace TorrentExtractor.Tests;

public class DownloadSettleShould
{
    [Fact]
    public async Task Wait_until_length_and_mtime_both_hold()
    {
        var written = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var reads = new Queue<PathSnapshot>([new(8, written), new(8, written)]);
        var delays = 0;

        var settled = await DownloadSettle.WaitAsync(
            () => reads.Dequeue(),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromHours(1),
            (_, _) =>
            {
                delays++;
                return Task.CompletedTask;
            },
            () => { },
            CancellationToken.None
        );

        Assert.True(settled);
        Assert.Equal(1, delays);
        Assert.Empty(reads);
    }

    [Fact]
    public async Task Keep_waiting_when_mtime_changes_and_length_does_not()
    {
        var first = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var second = first.AddSeconds(1);
        var reads = new Queue<PathSnapshot>([new(8, first), new(8, second), new(8, second)]);
        var delays = 0;

        var settled = await DownloadSettle.WaitAsync(
            () => reads.Dequeue(),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromHours(1),
            (_, _) =>
            {
                delays++;
                return Task.CompletedTask;
            },
            () => { },
            CancellationToken.None
        );

        Assert.True(settled);
        Assert.Equal(2, delays);
        Assert.Empty(reads);
    }

    [Fact]
    public async Task Keep_waiting_while_a_same_length_file_is_rewritten()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "movie.mkv");
        var firstWrite = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var secondWrite = firstWrite.AddMinutes(1);
        File.WriteAllBytes(path, [1, 1, 1, 1]);
        File.SetLastWriteTimeUtc(path, firstWrite);
        var reads = 0;

        var settled = await DownloadSettle.WaitAsync(
            () =>
            {
                reads++;
                if (reads == 2)
                {
                    File.WriteAllBytes(path, [2, 2, 2, 2]);
                    File.SetLastWriteTimeUtc(path, secondWrite);
                }

                return PathSnapshot.Capture(path);
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromHours(1),
            (_, _) => Task.CompletedTask,
            () => { },
            CancellationToken.None
        );

        Assert.True(settled);
        Assert.Equal(3, reads);
        Assert.Equal(4, PathSnapshot.Capture(path).Length);
        Assert.Equal(secondWrite, PathSnapshot.Capture(path).NewestWriteTimeUtc);
    }

    [Fact]
    public void Capture_directory_length_and_newest_contained_file()
    {
        using var dir = new TempDirectory();
        var older = Path.Combine(dir.Path, "a.mkv");
        var nested = Path.Combine(dir.Path, "season");
        Directory.CreateDirectory(nested);
        var newer = Path.Combine(nested, "b.mkv");
        var olderWrite = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newerWrite = olderWrite.AddHours(2);
        File.WriteAllBytes(older, [1]);
        File.WriteAllBytes(newer, [2, 2]);
        File.SetLastWriteTimeUtc(older, olderWrite);
        File.SetLastWriteTimeUtc(newer, newerWrite);

        var snap = PathSnapshot.Capture(dir.Path);

        Assert.Equal(new DirectoryInfo(dir.Path).Length(), snap.Length);
        Assert.Equal(3, snap.Length);
        Assert.Equal(newerWrite, snap.NewestWriteTimeUtc);
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
