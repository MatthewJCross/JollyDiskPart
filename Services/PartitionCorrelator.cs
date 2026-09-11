using System.Globalization;
using JollyDiskPart.Models;

namespace JollyDiskPart.Services
{
    public class PartitionCorrelator
    {
        public List<PartitionInfo> Merge(List<PartitionInfo> partitions, List<VolumeInfo> volumes)
        {
            foreach (var partition in partitions)
            {
                if (partition.VolumeNumber == null)
                    continue;

                var volume = volumes.FirstOrDefault(v => v.Number == partition.VolumeNumber);
                if (volume == null)
                    continue;

                partition.FileSystem = volume.FileSystem;
                partition.Label = volume.Label;
                partition.Used = volume.Used;
                partition.Free = volume.Free;
                partition.DriveLetter = volume.DriveLetter;
                partition.Status = string.Join(", ", new[] { volume.Status, volume.Info }.Where(s => !string.IsNullOrWhiteSpace(s)));                
                //partition.Status = volume.Status;
            }

            return partitions;
        }

        private long ParseSize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            text = text.Trim().ToUpperInvariant();

            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                return 0;

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return 0;

            switch (parts[1])
            {
                case "KB":
                    return (long)(value * 1024);

                case "MB":
                    return (long)(value * 1024 * 1024);

                case "GB":
                    return (long)(value * 1024 * 1024 * 1024);

                case "TB":
                    return (long)(value * 1024 * 1024 * 1024 * 1024);

                default:
                    return 0;
            }
        }
    }
}
