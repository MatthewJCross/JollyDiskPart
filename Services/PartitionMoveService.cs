using JollyDiskPart.Interop;
using JollyDiskPart.Models;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace JollyDiskPart.Services
{
    public sealed class PartitionMoveService
    {
        private readonly DiskService _diskService;
        private readonly VolumeMoveService _ntfsMoveService;

        public PartitionMoveService(DiskService diskService, VolumeMoveService ntfsMoveService)
        {
            _diskService = diskService;
            _ntfsMoveService = ntfsMoveService; 
        }

        private static bool IsSupportedMoveFileSystem(string? fileSystem)
        {
            return fileSystem?.Trim().ToUpperInvariant() switch
            {
                "NTFS" => true,
                "FAT32" => true,
                _ => false
            };
        }

        public Task<PartitionMoveResult> ValidateMoveAsync(DiskLayoutItem partition, DiskLayoutItem adjacent, long targetOffsetBytes)
        {
            if (partition.Partition.IsUnallocated)
                return Task.FromResult(PartitionMoveResult.Fail("The selected item is unallocated space."));

            if (partition.Partition.IsProtected)
                return Task.FromResult(PartitionMoveResult.Fail("Protected partitions cannot currently be moved."));

            long currentOffset = partition.OffsetBytes;
            long partitionSize = partition.Partition.SizeBytes;

            if (targetOffsetBytes == currentOffset)
                return Task.FromResult(PartitionMoveResult.Fail("The partition is already at the requested position."));

            bool movingLeft = targetOffsetBytes < currentOffset;

            if (!adjacent.Partition.IsUnallocated)
                return Task.FromResult(PartitionMoveResult.Fail(movingLeft ? "There is no unallocated space immediately before the partition." : "There is no unallocated space immediately after the partition."));

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
                return Task.FromResult(PartitionMoveResult.Fail("The requested position is outside the available unallocated space."));

            const long alignment = 1024L * 1024L;

            if (targetOffsetBytes % alignment != 0)
                return Task.FromResult(PartitionMoveResult.Fail("The requested position is not correctly aligned."));

            return Task.FromResult(PartitionMoveResult.Succeeded(currentOffset, targetOffsetBytes, partitionSize));
        }

        public async Task<PartitionMoveResult> MoveAsync(DiskLayoutItem partition, DiskLayoutItem adjacent, long targetOffsetBytes, bool verifyAfterCopy, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var validation = await ValidateMoveAsync(partition, adjacent, targetOffsetBytes);

            if (!validation.Success)
                return validation;

            if (!IsSupportedMoveFileSystem(partition.Partition.FileSystem))
                return PartitionMoveResult.Fail($"Moving {partition.Partition.FileSystem ?? "Unknown"} partitions is not currently supported.");

            var operation = new PartitionMoveOperation
            {
                SourceOffsetBytes = partition.OffsetBytes,
                DestinationOffsetBytes = targetOffsetBytes,
                LengthBytes = partition.Partition.SizeBytes
            };

            cancellationToken.ThrowIfCancellationRequested();

            var sectorMover = new PartitionSectorMover();
            var range = sectorMover.VerifyMove(operation.SourceOffsetBytes, operation.DestinationOffsetBytes, operation.LengthBytes);

            if (!range.IsValid)
                return PartitionMoveResult.Fail(range.Error);

            int diskNumber = partition.DiskNumber;
            string fileSystem = partition.Partition.FileSystem?.Trim().ToUpperInvariant() ?? "UNKNOWN";

            LockedVolume? volumeLock = null;

            try
            {
                if (!string.IsNullOrWhiteSpace(partition.Partition.DriveLetter))
                {
                    volumeLock = _ntfsMoveService.LockAndDismountVolume(partition.Partition.DriveLetter);
                }

                cancellationToken.ThrowIfCancellationRequested();

                var layout = DiskPartitionNative.GetDriveLayout(diskNumber);

                if (operation.MovingLeft)
                    await sectorMover.MoveLeftAsync(diskNumber, operation.SourceOffsetBytes, operation.DestinationOffsetBytes, operation.LengthBytes, verifyAfterCopy, progress, cancellationToken);
                else
                    await sectorMover.MoveRightAsync(diskNumber, operation.SourceOffsetBytes, operation.DestinationOffsetBytes, operation.LengthBytes, verifyAfterCopy, progress, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                DiskPartitionNative.MovePartition(diskNumber, partition.Partition.Number, operation.DestinationOffsetBytes);

                var finalLayout = DiskPartitionNative.GetDriveLayout(diskNumber);
                var movedPartition = finalLayout.Partitions.FirstOrDefault(p => p.PartitionNumber == partition.Partition.Number);

                if (movedPartition == null)
                    return PartitionMoveResult.Fail("Partition disappeared after updating the partition table.");

                if (movedPartition.StartingOffset != operation.DestinationOffsetBytes)
                    return PartitionMoveResult.Fail($"Partition table verification failed.\n\nExpected offset: {operation.DestinationOffsetBytes:N0}\nActual offset:   {movedPartition.StartingOffset:N0}");

                if (movedPartition.PartitionLength != operation.LengthBytes)
                    return PartitionMoveResult.Fail($"Partition length changed unexpectedly.\n\nExpected length: {operation.LengthBytes:N0}\nActual length:   {movedPartition.PartitionLength:N0}");

                return PartitionMoveResult.Succeeded(operation.SourceOffsetBytes, operation.DestinationOffsetBytes, operation.LengthBytes);
            }
            catch (OperationCanceledException)
            {
                return PartitionMoveResult.Fail("Partition movement was cancelled.");
            }
            catch (Exception ex)
            {
                return PartitionMoveResult.Fail($"Partition movement failed:\n\nType: {ex.GetType().FullName}\nMessage: {ex.Message}\n\nSource: {operation.SourceOffsetBytes:N0}\nDestination: {operation.DestinationOffsetBytes:N0}");
            }
            finally
            {
                if (volumeLock != null)
                {
                    try
                    {
                        _ntfsMoveService.UnlockVolume(volumeLock);
                    }
                    catch (Exception ex)
                    {
                    }

                    volumeLock.Dispose();
                }
            }
        }
    }
}
