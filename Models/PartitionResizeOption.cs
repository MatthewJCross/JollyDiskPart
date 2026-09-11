namespace JollyDiskPart.Models
{
    public class PartitionResizeOptions
    {
        public PartitionInfo? Partition { get; set; }
        public long OriginalSizeMB { get; set; }
        public long NewSizeMB { get; set; }
        public long MinimumSizeMB { get; set; }
        public long MaximumSizeMB { get; set; }
        public long AlignmentMB { get; set; } = 1;
    }
}
