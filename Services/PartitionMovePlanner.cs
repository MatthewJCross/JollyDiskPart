using JollyDiskPart.Models;

namespace JollyDiskPart.Services
{
    public sealed class PartitionMovePlanner
    {
        private const long AlignmentBytes = 1024L * 1024L;

        public PartitionMovePlan CreatePlan(DiskLayoutItem partition, DiskLayoutItem adjacent, long targetOffsetBytes)
        {
            if (partition.Partition.IsUnallocated)
                return PartitionMovePlan.Invalid("The selected item is unallocated space.");

            if (partition.Partition.IsProtected)
                return PartitionMovePlan.Invalid("Protected partitions cannot be moved.");

            long currentOffset = partition.OffsetBytes;

            if (targetOffsetBytes == currentOffset)
                return PartitionMovePlan.Invalid("The partition is already at the requested position.");

            bool movingLeft = targetOffsetBytes < currentOffset;

            if (!adjacent.Partition.IsUnallocated)
                return PartitionMovePlan.Invalid(movingLeft ? "There is no unallocated space immediately before the partition." : "There is no unallocated space immediately after the partition.");

            long availableStart;
            long availableEnd;

            if (movingLeft)
            {
                availableStart = adjacent.OffsetBytes;
                availableEnd = currentOffset;
            }
            else
            {
                availableStart = currentOffset;
                availableEnd = adjacent.OffsetBytes + adjacent.Partition.SizeBytes;
            }

            if (targetOffsetBytes < availableStart || targetOffsetBytes > availableEnd)
                return PartitionMovePlan.Invalid("The target position is outside the available space.");

            if (targetOffsetBytes % AlignmentBytes != 0)
                return PartitionMovePlan.Invalid("The target position is not correctly aligned.");

            return PartitionMovePlan.Valid(currentOffset, targetOffsetBytes, partition.Partition.SizeBytes);
        }
    }
}
