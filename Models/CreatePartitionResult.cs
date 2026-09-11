namespace JollyDiskPart.Models
{
    public class CreatePartitionResult
    {
        public bool Success => Partition != null && string.IsNullOrWhiteSpace(Error);
        public PartitionInfo? Partition { get; init; }
        public string? Error { get; init; }
        public string? Output { get; init; }
    }
}
