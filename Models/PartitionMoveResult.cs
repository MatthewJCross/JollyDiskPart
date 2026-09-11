namespace JollyDiskPart.Models
{
    public sealed class PartitionMoveResult
    {
        public bool Success { get; init; }
        public string? Error { get; init; }
        public long OriginalOffsetBytes { get; init; }
        public long NewOffsetBytes { get; init; }
        public long SizeBytes { get; init; }

        public long BytesMoved => Math.Abs(OriginalOffsetBytes - NewOffsetBytes);

        public static PartitionMoveResult Fail(string error)
        {
            return new PartitionMoveResult
            {
                Success = false,
                Error = error
            };
        }

        public static PartitionMoveResult Succeeded(long originalOffsetBytes, long newOffsetBytes, long sizeBytes)
        {
            return new PartitionMoveResult
            {
                Success = true,
                OriginalOffsetBytes = originalOffsetBytes,
                NewOffsetBytes = newOffsetBytes,
                SizeBytes = sizeBytes
            };
        }
    }
}
