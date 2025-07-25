using System.ComponentModel.DataAnnotations;

namespace TorrentExtractor.Settings;

public class Core
{
    /// <summary>
    /// To determine if the file has been fully copied, the length of the file is compared between a given interval. If the lengths are equal, then the copy process starts. Default is 15 seconds. Must be 1 or greater.
    /// </summary>
    public int FileCompareInterval { get; set; } = 15;

    public void Validate()
    {
        if (FileCompareInterval < 1)
            throw new ValidationException($"A valid {nameof(FileCompareInterval)} is required!");
    }

    public override string ToString()
    {
        return $"{nameof(FileCompareInterval)}={FileCompareInterval}";
    }
}
