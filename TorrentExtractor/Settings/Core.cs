using System.ComponentModel.DataAnnotations;

namespace TorrentExtractor.Settings;

public class Core
{
    /// <summary>
    /// To determine if the download has settled, the length and last-write time are compared across this interval. Copying starts when both are unchanged. Default is 15 seconds. Must be 1 or greater.
    /// </summary>
    public int FileCompareInterval { get; set; } = 15;

    /// <summary>
    /// Maximum time to wait for a download to settle before skipping it. Default is 12 hours. Must be 1 or greater.
    /// </summary>
    public int MaxSettleHours { get; set; } = 12;

    public void Validate()
    {
        if (FileCompareInterval < 1)
            throw new ValidationException($"A valid {nameof(FileCompareInterval)} is required!");

        if (MaxSettleHours < 1)
            throw new ValidationException($"A valid {nameof(MaxSettleHours)} is required!");
    }

    public override string ToString()
    {
        return $"{nameof(FileCompareInterval)}={FileCompareInterval}, {nameof(MaxSettleHours)}={MaxSettleHours}";
    }
}
