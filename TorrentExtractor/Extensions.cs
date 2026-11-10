using System.IO;

namespace TorrentExtractor;

public static class Extensions
{
    public static long Length(this DirectoryInfo dir) => PathSnapshot.CaptureDirectory(dir).Length;
}
