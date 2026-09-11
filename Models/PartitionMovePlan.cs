namespace JollyDiskPart.Models
{
    public sealed class PartitionMovePlan
    {
        public bool IsValid { get; init; }
        public string? Error { get; init; }
        public long CurrentOffsetBytes { get; init; }
        public long TargetOffsetBytes { get; init; }
        public long PartitionSizeBytes { get; init; }

        public long MoveDistanceBytes =>  Math.Abs(CurrentOffsetBytes - TargetOffsetBytes);
        public long MoveDistanceMB => MoveDistanceBytes / (1024 * 1024);

        public string? FileSystem { get; set; }
        public long? ClusterSizeBytes { get; set; }
        public long? TotalClusters { get; init; }
        public long? AllocatedClusters { get; init; }
        public long? FreeClusters { get; init; }
        
        public static PartitionMovePlan Invalid(string error)
        {
            return new PartitionMovePlan
            {
                IsValid = false,
                Error = error
            };
        }

        public static PartitionMovePlan Valid(long currentOffsetBytes, long targetOffsetBytes, long partitionSizeBytes)//, string fileSystem, long clusterSizeBytes, long totalClusters, long allocatedClusters)
        {
            return new PartitionMovePlan
            {
                IsValid = true,
                CurrentOffsetBytes = currentOffsetBytes,
                TargetOffsetBytes = targetOffsetBytes,
                PartitionSizeBytes = partitionSizeBytes
                //FileSystem = fileSystem,
                //ClusterSizeBytes = clusterSizeBytes,
                //TotalClusters = totalClusters,
                //AllocatedClusters = allocatedClusters,
                //FreeClusters = totalClusters - allocatedClusters
            };
        }
    }
}
