namespace JollyDiskPart.Models
{
    public sealed class PartitionMoveRange
    {
        public bool IsValid { get; init; }
        public string? Error { get; init; }
        public long SourceOffsetBytes { get; init; }
        public long SourceEndBytes { get; init; }
        public long DestinationOffsetBytes { get; init; }
        public long DestinationEndBytes { get; init; }
        public long LengthBytes { get; init; }
        public long SectorSize { get; init; }
        public bool MovingLeft { get; init; }
        public bool MovingRight { get; init; }
        public bool Overlaps { get; init; }

        public long MoveDistanceBytes => Math.Abs(SourceOffsetBytes - DestinationOffsetBytes);
        public long MoveDistanceMB => MoveDistanceBytes / (1024L * 1024L);

        public static PartitionMoveRange Valid(long sourceOffsetBytes, long sourceEndBytes, long destinationOffsetBytes, long destinationEndBytes, long lengthBytes, long sectorSize, bool movingLeft, bool movingRight, bool overlaps)
        {
            return new PartitionMoveRange
            {
                IsValid = true,
                SourceOffsetBytes = sourceOffsetBytes,
                SourceEndBytes = sourceEndBytes,
                DestinationOffsetBytes = destinationOffsetBytes,
                DestinationEndBytes = destinationEndBytes,
                LengthBytes = lengthBytes,
                SectorSize = sectorSize,
                MovingLeft = movingLeft,
                MovingRight = movingRight,
                Overlaps = overlaps
            };
        }

        public static PartitionMoveRange Fail(string error)
        {
            return new PartitionMoveRange
            {
                IsValid = false,
                Error = error
            };
        }
    }
}
