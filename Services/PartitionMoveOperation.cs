namespace JollyDiskPart.Services
{
    public sealed class PartitionMoveOperation
    {
        public long SourceOffsetBytes { get; init; }
        public long DestinationOffsetBytes { get; init; }
        public long LengthBytes { get; init; }

        public long BytesToMove => LengthBytes;

        public bool MovingLeft => DestinationOffsetBytes < SourceOffsetBytes;
        public bool MovingRight => DestinationOffsetBytes > SourceOffsetBytes;
    }
}
