using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace TorrentExtractor.Settings
{
    public class Core
    {
        /// <summary>
        /// Delay in seconds before copying the file from the source folder. Default is 60 seconds. Must be over 0.
        /// </summary>
        public int CopyDelay { get; set; } = 60;

        public void Validate()
        {
            if(CopyDelay <= 0)
                throw new ValidationException($"A valid {nameof(CopyDelay)} is required!");
        }
        
        public override string ToString()
        {
            return $"CopyDelay={CopyDelay}";
        }
    }
}