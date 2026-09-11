namespace JollyDiskPart.Models
{
    public sealed class PartitionGeometry
    {
        public int PartitionNumber { get; init; }
        public long OffsetBytes { get; init; }
        public long SizeBytes { get; init; }
    }
}
