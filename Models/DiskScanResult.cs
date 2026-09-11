namespace JollyDiskPart.Models
{
    public class DiskScanResult
    {
        public List<DiskInfo> Disks { get; } = new();
        public List<PartitionInfo> Partitions { get; } = new();
        public List<VolumeInfo> Volumes { get; } = new();
    }
}
