using System.IO;
using System.Linq;

namespace TorrentExtractor;

public static class Extensions
{
    public static long Length(this DirectoryInfo dir) =>
        dir.GetFiles().Sum(fi => fi.Length) + dir.GetDirectories().Sum(di => di.Length());
}
