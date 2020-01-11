using System.ComponentModel.DataAnnotations;

namespace TorrentExtractor.Settings
{
    public class General
    {
        public int FileCopyDelaySeconds { get; set; }

        public void Validate()
        {
            if(FileCopyDelaySeconds <= 0)
                throw new ValidationException($"A valid {nameof(FileCopyDelaySeconds)} is required!");
        }
        
        public override string ToString()
        {
            return $"FileCopyDelaySeconds={FileCopyDelaySeconds}";
        }
    }
}