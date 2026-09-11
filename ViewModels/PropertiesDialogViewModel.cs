using JollyDiskPart.Models;

namespace JollyDiskPart.ViewModels
{
    public class PropertiesDialogViewModel : ViewModelBase
    {
        public PartitionInfo Partition { get; }

        public long OffsetBytes { get; }

        public PropertiesDialogViewModel(PartitionInfo partition, long offsetBytes = 0)
        {
            Partition = partition;
            OffsetBytes = offsetBytes;
        }

        // ===== Partition =====

        public string PartitionNumber => Partition.Number.ToString();

        public string DriveLetter => string.IsNullOrWhiteSpace(Partition.DriveLetter) ? "(None)" : $"{Partition.DriveLetter}:";

        public string Label => string.IsNullOrWhiteSpace(Partition.Label) ? "(None)" : Partition.Label;

        public string FileSystem => string.IsNullOrWhiteSpace(Partition.FileSystem) ? "Unknown" : Partition.FileSystem;

        public string Type => string.IsNullOrWhiteSpace(Partition.Type) ? "Unknown" : Partition.Type;

        public string TypeId => string.IsNullOrWhiteSpace(Partition.TypeId) ? "N/A" : Partition.TypeId;

        public string VolumeNumber => Partition.VolumeNumber.HasValue ? Partition.VolumeNumber.Value.ToString() : "N/A";


        // ===== Layout =====

        public string Size => FormatBytes(Partition.SizeBytes);

        public string Offset => FormatBytes(OffsetBytes);

        public string OffsetBytesDisplay => $"{OffsetBytes:N0} bytes";

        public string EndOffset => FormatBytes(OffsetBytes + Partition.SizeBytes);

        public string EndOffsetBytesDisplay => $"{OffsetBytes + Partition.SizeBytes:N0} bytes";


        // ===== Filesystem =====

        public string Used => string.IsNullOrWhiteSpace(Partition.FileSystem) ? "N/A" : string.IsNullOrWhiteSpace(Partition.Used) ? "N/A" : Partition.Used;

        public string Free => string.IsNullOrWhiteSpace(Partition.FileSystem) ? "N/A" : string.IsNullOrWhiteSpace(Partition.Free) ? "N/A" : Partition.Free;


        // ===== Flags =====

        public string Hidden => Partition.Hidden ? "Yes" : "No";

        public string Required => Partition.Required ? "Yes" : "No";

        public string ReadOnly => Partition.ReadOnly ? "Yes" : "No";

        public string System => Partition.IsSystem ? "Yes" : "No";

        public string Boot => Partition.IsBoot ? "Yes" : "No";

        public string Recovery => Partition.IsRecovery ? "Yes" : "No";

        public string OEM => Partition.IsOEM ? "Yes" : "No";

        public string Protected => Partition.IsProtected ? "Yes" : "No";


        private static string FormatBytes(long bytes)
        {
            if (bytes < 0)
                return "N/A";

            double value = bytes;
            string[] units = { "B", "KB", "MB", "GB", "TB" };

            int unit = 0;

            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return $"{value:0.##} {units[unit]}";
        }
    }
}
