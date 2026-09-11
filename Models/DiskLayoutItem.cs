namespace JollyDiskPart.Models
{
    public sealed class DiskLayoutItem
    {
        public int DiskNumber { get; init; }
        public PartitionInfo Partition { get; init; } = null!;
        public long OffsetBytes { get; init; }
        public long EndBytes => OffsetBytes + Partition.SizeBytes;
    }
}
