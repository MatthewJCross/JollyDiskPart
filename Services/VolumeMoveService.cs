using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using JollyDiskPart.Interop;
using JollyDiskPart.Models;

namespace JollyDiskPart.Services
{
    public sealed class VolumeMoveService
    {
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;

        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;

        private const uint OPEN_EXISTING = 3;

        private const int BufferSize = 8 * 1024 * 1024;
        private const int ErrorMoreData = 234;

        public bool CanOpenVolume(string driveLetter)
        {
            if (string.IsNullOrWhiteSpace(driveLetter))
                return false;

            string path = $@"\\.\{driveLetter.TrimEnd(':')}:";

            using var handle = VolumeNative.CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

            return !handle.IsInvalid;
        }

        public VolumeBitmap GetVolumeBitmap(string driveLetter)
        {
            var raw = GetVolumeBitmapBuffer(driveLetter);
            return VolumeBitmapParser.Parse(raw);
        }

        private byte[] GetVolumeBitmapBuffer(string driveLetter)
        {
            string volumePath = $@"\\.\{driveLetter.TrimEnd(':')}:";

            using var handle = VolumeNative.CreateFile(volumePath, VolumeNative.GenericRead, VolumeNative.FileShareRead | VolumeNative.FileShareWrite, IntPtr.Zero, VolumeNative.OpenExisting, 0, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to open volume {driveLetter}:");
            }

            int inputSize = Marshal.SizeOf<VolumeNative.STARTING_LCN_INPUT_BUFFER>();
            IntPtr inputPtr = Marshal.AllocHGlobal(inputSize);
            IntPtr outputPtr = Marshal.AllocHGlobal(BufferSize);

            try
            {
                long nextLcn = 0;
                long totalClusters = 0;
                long firstStartingLcn = -1;

                using var bitmapStream = new MemoryStream();

                while (true)
                {
                    var input = new VolumeNative.STARTING_LCN_INPUT_BUFFER
                    {
                        StartingLcn = nextLcn
                    };

                    Marshal.StructureToPtr(input, inputPtr, false);
                    bool success = VolumeNative.DeviceIoControl(handle, VolumeNative.FSCTL_GET_VOLUME_BITMAP, inputPtr, (uint)inputSize, outputPtr, BufferSize, out uint bytesReturned, IntPtr.Zero);
                    int error = success ? 0 : Marshal.GetLastWin32Error();

                    if (!success && error != ErrorMoreData)
                    {
                        throw new Win32Exception(error, $"FSCTL_GET_VOLUME_BITMAP failed. Win32 error: {error} (0x{error:X8})");
                    }

                    if (bytesReturned < 16)
                    {
                        throw new InvalidDataException("Invalid volume bitmap response.");
                    }

                    // First 8 bytes = StartingLcn
                    long startingLcn = Marshal.ReadInt64(outputPtr, 0);

                    // Next 8 bytes = BitmapSize
                    long bitmapSize = Marshal.ReadInt64(outputPtr, 8);
                    if (bitmapSize <= 0)
                    {
                        throw new InvalidDataException("Volume bitmap returned an invalid bitmap size.");
                    }

                    if (firstStartingLcn < 0)
                        firstStartingLcn = startingLcn;

                    long availableBytes = bytesReturned - 16;
                    if (availableBytes <= 0)
                    {
                        throw new InvalidDataException("Volume bitmap returned no bitmap data.");
                    }

                    long expectedBitmapBytes = (bitmapSize + 7) / 8;
                    long actualBitmapBytes = Math.Min(expectedBitmapBytes, availableBytes);
                    var chunk = new byte[actualBitmapBytes];
                    Marshal.Copy(IntPtr.Add(outputPtr, 16), chunk, 0, (int)actualBitmapBytes);
                    bitmapStream.Write(chunk, 0, chunk.Length);

                    // Work out how many clusters this particular response contains.
                    long clustersReturned = actualBitmapBytes * 8;

                    // Don't advance beyond the actual BitmapSize.
                    clustersReturned = Math.Min(clustersReturned, bitmapSize);
                    totalClusters += clustersReturned;
                    long newNextLcn = startingLcn + clustersReturned;
                    
                    if (!success && newNextLcn <= nextLcn)
                    {
                        throw new InvalidDataException($"Volume bitmap did not advance. Current LCN={nextLcn:N0}, Starting LCN={startingLcn:N0}, Clusters returned={clustersReturned:N0}.");
                    }

                    nextLcn = newNextLcn;
                    
                    if (success)
                        break;
                }

                byte[] bitmap = bitmapStream.ToArray();

                // Rebuild the structure expected by VolumeBitmapParser:
                //
                // Offset 0  = StartingLcn
                // Offset 8  = BitmapSize
                // Offset 16 = bitmap
                //
                using var resultStream = new MemoryStream();
                using (var writer = new BinaryWriter(resultStream, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write(firstStartingLcn);
                    writer.Write(totalClusters);
                    writer.Write(bitmap);
                }

                return resultStream.ToArray();
            }
            finally
            {
                Marshal.FreeHGlobal(inputPtr);
                Marshal.FreeHGlobal(outputPtr);
            }
        }

        public VolumeInfo GetVolumeInfo(string driveLetter)
        {
            string root = $"{driveLetter.TrimEnd(':')}:\\";

            var volume = new VolumeInfo
            {
                DriveLetter = driveLetter
            };

            bool success = VolumeNative.GetDiskFreeSpace(root, out uint sectorsPerCluster, out uint bytesPerSector, out uint freeClusters, out uint totalClusters);

            if (success)
            {
                volume.SectorsPerCluster = sectorsPerCluster;
                volume.BytesPerSector = bytesPerSector;
                volume.FreeClusters = freeClusters;
                volume.TotalClusters = totalClusters;
            }

            return volume;
        }

        public string GetVolumeBitmapDiagnostic(string driveLetter)
        {
            if (string.IsNullOrWhiteSpace(driveLetter))
                return "No drive letter assigned.";

            try
            {
                var volumeInfo = GetVolumeInfo(driveLetter);
                var bitmap = GetVolumeBitmap(driveLetter);

                long expectedBitmapBytes = (bitmap.BitmapSize + 7) / 8;
                long actualBitmapBytes = bitmap.Bitmap.Length;
                double coverage = expectedBitmapBytes > 0 ? (double)actualBitmapBytes / expectedBitmapBytes * 100.0 : 0;

                return  $"Drive: {driveLetter}:\n" +
                        $"Filesystem: NTFS\n" +
                        $"Cluster size: {volumeInfo.BytesPerCluster:N0} bytes\n" +
                        $"Cluster size: {volumeInfo.ClusterSizeKB:N0} KB\n" +
                        $"Starting LCN: {bitmap.StartingLcn:N0}\n" +
                        $"Total clusters: {bitmap.BitmapSize:N0}\n" +
                        $"Allocated clusters: {bitmap.AllocatedClusters:N0}\n" +
                        $"Free clusters: {bitmap.FreeClusters:N0}\n" +
                        $"Expected bitmap: {expectedBitmapBytes:N0} bytes\n" +
                        $"Actual bitmap: {actualBitmapBytes:N0} bytes\n" +
                        $"Bitmap coverage: {coverage:0.00}%";
            }
            catch (Exception ex)
            {
                return $"Unable to read volume bitmap for {driveLetter}:\n\n" + ex.Message;
            }
        }

        public NtfsMovePreparation PrepareMove(string driveLetter)
        {
            if (string.IsNullOrWhiteSpace(driveLetter))
            {
                return NtfsMovePreparation.Fail("The partition does not have a drive letter.");
            }

            try
            {
                var volumeInfo = GetVolumeInfo(driveLetter);
                var bitmap = GetVolumeBitmap(driveLetter);

                if (volumeInfo.BytesPerCluster == 0)
                {
                    return NtfsMovePreparation.Fail("Unable to determine the NTFS cluster size.");
                }

                if (bitmap.BitmapSize <= 0)
                {
                    return NtfsMovePreparation.Fail("The NTFS volume bitmap contains no clusters.");
                }

                return NtfsMovePreparation.Success(volumeInfo, bitmap);
            }
            catch (Exception ex)
            {
                return NtfsMovePreparation.Fail($"Unable to prepare NTFS volume for moving:\n\n{ex.Message}");
            }
        }

        public SafeFileHandle OpenVolumeForMove(string driveLetter)
        {
            if (string.IsNullOrWhiteSpace(driveLetter))
                throw new ArgumentException("Drive letter is required.", nameof(driveLetter));

            string volumePath = $@"\\.\{driveLetter.TrimEnd(':')}:";

            var handle = VolumeNative.CreateFile(volumePath, VolumeNative.GenericRead | VolumeNative.GenericWrite, VolumeNative.FileShareRead | VolumeNative.FileShareWrite, IntPtr.Zero, VolumeNative.OpenExisting, 0, IntPtr.Zero);

            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();

                throw new Win32Exception(error, $"Unable to open volume {driveLetter}:");
            }

            return handle;
        }

        //public LockedVolume LockAndDismount(string driveLetter)
        //{
        //    if (string.IsNullOrWhiteSpace(driveLetter))
        //        throw new ArgumentException("A drive letter is required.", nameof(driveLetter));

        //    return LockAndDismountVolume(driveLetter);
        //}

        public LockedVolume LockAndDismountVolume(string driveLetter)
        {
            if (string.IsNullOrWhiteSpace(driveLetter))
                throw new ArgumentException("A drive letter is required.", nameof(driveLetter));

            string volumePath = $@"\\.\{driveLetter.TrimEnd(':')}:";
            SafeFileHandle handle = VolumeNative.CreateFile(volumePath, VolumeNative.GenericRead | VolumeNative.GenericWrite, VolumeNative.FileShareRead | VolumeNative.FileShareWrite, IntPtr.Zero, VolumeNative.OpenExisting, 0, IntPtr.Zero);

            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error, $"Unable to open volume {driveLetter}:");
            }

            try
            {
                bool locked = VolumeNative.DeviceIoControl(handle, VolumeNative.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
                if (!locked)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new Win32Exception(error, "Unable to lock the volume. The volume may be in use.");
                }

                Debug.WriteLine($"Volume {driveLetter}: successfully locked.");

                bool dismounted = VolumeNative.DeviceIoControl(handle, VolumeNative.FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
                if (!dismounted)
                {
                    int error = Marshal.GetLastWin32Error();

                    // Unlock because the lock succeeded.
                    VolumeNative.DeviceIoControl(handle, VolumeNative.FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
                    throw new Win32Exception(error, "Unable to dismount the volume.");
                }

                Debug.WriteLine($"Volume {driveLetter}: successfully dismounted.");

                // Transfer ownership of the handle.
                return new LockedVolume(handle);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        public void UnlockVolume(LockedVolume volume)
        {
            if (volume == null)
                throw new ArgumentNullException(nameof(volume));

            bool unlocked = VolumeNative.DeviceIoControl(volume.Handle, VolumeNative.FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
            if (!unlocked)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, "Unable to unlock volume.");
            }
        }

        public bool LockVolume(string driveLetter, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(driveLetter))
            {
                error = "No drive letter was supplied.";
                return false;
            }

            string volumePath = $@"\\.\{driveLetter.TrimEnd(':')}:";

            using var handle = VolumeNative.CreateFile(volumePath, VolumeNative.GenericRead | VolumeNative.GenericWrite, VolumeNative.FileShareRead | VolumeNative.FileShareWrite, IntPtr.Zero, VolumeNative.OpenExisting, 0, IntPtr.Zero);

            if (handle.IsInvalid)
            {
                error = new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to open volume {driveLetter}:").Message;
                return false;
            }

            uint bytesReturned;
            bool locked = VolumeNative.DeviceIoControl(handle, VolumeNative.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);

            if (!locked)
            {
                error = new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to lock volume {driveLetter}:").Message;
                return false;
            }

            return true;
        }

        public string TestLockAndDismount(string driveLetter)
        {
            LockedVolume? volume = null;

            try
            {
                volume = LockAndDismountVolume(driveLetter);
                return $"Volume {driveLetter}: was successfully locked and dismounted.\n\nThe volume remains locked until the test releases it.";
            }
            catch (Exception ex)
            {
                return $"Unable to lock/dismount volume {driveLetter}:\n\n" + ex.Message;
            }
            finally
            {
                if (volume != null)
                {
                    try
                    {
                        UnlockVolume(volume);
                    }
                    catch
                    {
                        // The original error is more useful than a cleanup error.
                    }
                    volume.Dispose();
                }
            }
        }
    }
}
