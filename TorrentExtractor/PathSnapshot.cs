using System;
using System.IO;

namespace TorrentExtractor;

public readonly record struct PathSnapshot(long Length, DateTime NewestWriteTimeUtc)
{
    public static PathSnapshot Capture(string path)
    {
        if (Directory.Exists(path))
        {
            return CaptureDirectory(new DirectoryInfo(path));
        }

        var file = new FileInfo(path);
        return new PathSnapshot(file.Length, file.LastWriteTimeUtc);
    }

    public static PathSnapshot CaptureDirectory(DirectoryInfo dir)
    {
        long length = 0;
        var newest = DateTime.MinValue;

        foreach (var file in dir.GetFiles())
        {
            length += file.Length;
            var written = file.LastWriteTimeUtc;
            if (written > newest)
            {
                newest = written;
            }
        }

        foreach (var sub in dir.GetDirectories())
        {
            var snap = CaptureDirectory(sub);
            length += snap.Length;
            if (snap.NewestWriteTimeUtc > newest)
            {
                newest = snap.NewestWriteTimeUtc;
            }
        }

        if (newest == DateTime.MinValue)
        {
            newest = dir.LastWriteTimeUtc;
        }

        return new PathSnapshot(length, newest);
    }
}
