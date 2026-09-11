using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JollyDiskPart.Interop
{
    internal static class DiskPartitionNative
    {
        private const uint IOCTL_DISK_GET_DRIVE_LAYOUT_EX = 0x00070050;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        private const uint PARTITION_STYLE_MBR = 0;
        private const uint PARTITION_STYLE_GPT = 1;

        private const uint IOCTL_DISK_SET_DRIVE_LAYOUT_EX = 0x0007C054;
        private const uint IOCTL_DISK_UPDATE_PROPERTIES = 0x00070140;
        
        [StructLayout(LayoutKind.Sequential)]
        private struct DRIVE_LAYOUT_INFORMATION_EX
        {
            public uint PartitionStyle;
            public uint PartitionCount;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
            public byte[] DriveLayoutInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PARTITION_INFORMATION_EX
        {
            public uint PartitionStyle;
            public long StartingOffset;
            public long PartitionLength;
            public uint PartitionNumber;
            public byte RewritePartition;
            public byte Reserved1;
            public ushort Reserved2;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 112)]
            public byte[] PartitionInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GPT_PARTITION_INFORMATION
        {
            public Guid PartitionType;
            public Guid PartitionId;
            public ulong Attributes;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
            public char[] Name;
        }

        public sealed class PartitionLayoutEntry
        {
            public uint PartitionNumber { get; init; }
            public long StartingOffset { get; init; }
            public long PartitionLength { get; init; }
            public Guid PartitionId { get; init; }
            public Guid PartitionType { get; init; }
            public ulong Attributes { get; init; }
            public string Name { get; init; } = string.Empty;
            public long EndingOffset => StartingOffset + PartitionLength;
            public override string ToString() => $"Partition {PartitionNumber}: Offset={StartingOffset:N0}, Length={PartitionLength:N0}, End={EndingOffset:N0}, Type={PartitionType}, Id={PartitionId}, Attributes=0x{Attributes:X}, Name=\"{Name}\"";
        }

        public sealed class DriveLayout
        {
            public uint PartitionStyle { get; init; }
            public uint PartitionCount { get; init; }
            public bool IsGPT => PartitionStyle == PARTITION_STYLE_GPT;
            public bool IsMBR => PartitionStyle == PARTITION_STYLE_MBR;
            public List<PartitionLayoutEntry> Partitions { get; } = new();
        }

        public static DriveLayout GetDriveLayout(int diskNumber)
        {
            using SafeFileHandle handle = OpenPhysicalDrive(diskNumber);

            int bufferSize = 4096;

            while (true)
            {
                IntPtr buffer = Marshal.AllocHGlobal(bufferSize);

                try
                {
                    bool success = DeviceIoControl(handle, IOCTL_DISK_GET_DRIVE_LAYOUT_EX, IntPtr.Zero, 0, buffer, (uint)bufferSize, out uint bytesReturned, IntPtr.Zero);

                    if (success)
                        return ParseLayout(buffer);

                    int error = Marshal.GetLastWin32Error();

                    if (error != 122)
                        throw new Win32Exception(error, $"IOCTL_DISK_GET_DRIVE_LAYOUT_EX failed. Win32 error: {error}");

                    bufferSize *= 2;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }

        private static DriveLayout ParseLayout(IntPtr buffer)
        {
            var header = Marshal.PtrToStructure<DRIVE_LAYOUT_INFORMATION_EX>(buffer);

            var result = new DriveLayout
            {
                PartitionStyle = header.PartitionStyle,
                PartitionCount = header.PartitionCount
            };

            int headerSize = Marshal.SizeOf<DRIVE_LAYOUT_INFORMATION_EX>();
            int partitionSize = Marshal.SizeOf<PARTITION_INFORMATION_EX>();
            IntPtr partitionPtr = IntPtr.Add(buffer, headerSize);

            for (uint i = 0; i < header.PartitionCount; i++)
            {
                var partition = Marshal.PtrToStructure<PARTITION_INFORMATION_EX>(partitionPtr);

                if (partition.PartitionNumber != 0 && partition.PartitionLength > 0)
                {
                    var entry = new PartitionLayoutEntry
                    {
                        PartitionNumber = partition.PartitionNumber,
                        StartingOffset = partition.StartingOffset,
                        PartitionLength = partition.PartitionLength
                    };

                    if (partition.PartitionStyle == PARTITION_STYLE_GPT && partition.PartitionInformation != null)
                    {
                        IntPtr gptPtr = Marshal.AllocHGlobal(112);

                        try
                        {
                            Marshal.Copy(partition.PartitionInformation, 0, gptPtr, 112);
                            var gpt = Marshal.PtrToStructure<GPT_PARTITION_INFORMATION>(gptPtr);

                            entry = new PartitionLayoutEntry
                            {
                                PartitionNumber = partition.PartitionNumber,
                                StartingOffset = partition.StartingOffset,
                                PartitionLength = partition.PartitionLength,
                                PartitionId = gpt.PartitionId,
                                PartitionType = gpt.PartitionType,
                                Attributes = gpt.Attributes,
                                Name = gpt.Name == null ? string.Empty : new string(gpt.Name).TrimEnd('\0')
                            };
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(gptPtr);
                        }
                    }

                    result.Partitions.Add(entry);
                }

                partitionPtr = IntPtr.Add(partitionPtr, partitionSize);
            }

            return result;
        }

        private static SafeFileHandle OpenPhysicalDrive(int diskNumber)
        {
            string path = $@"\\.\PhysicalDrive{diskNumber}";
            SafeFileHandle handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error, $"Unable to open {path}. Win32 error: {error}");
            }

            return handle;
        }

        public static void MovePartition(int diskNumber, int partitionNumber, long newStartingOffset)
        {
            if (newStartingOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(newStartingOffset));

            using SafeFileHandle handle = OpenPhysicalDrive(diskNumber);

            int bufferSize = 4096;

            while (true)
            {
                IntPtr buffer = Marshal.AllocHGlobal(bufferSize);

                try
                {
                    bool success = DeviceIoControl(handle, IOCTL_DISK_GET_DRIVE_LAYOUT_EX, IntPtr.Zero, 0, buffer, (uint)bufferSize, out uint bytesReturned, IntPtr.Zero);

                    if (!success)
                    {
                        int error = Marshal.GetLastWin32Error();

                        if (error == 122)
                        {
                            bufferSize *= 2;
                            continue;
                        }

                        throw new Win32Exception(error, $"IOCTL_DISK_GET_DRIVE_LAYOUT_EX failed. Win32 error: {error}");
                    }

                    var header = Marshal.PtrToStructure<DRIVE_LAYOUT_INFORMATION_EX>(buffer);

                    int headerSize = Marshal.SizeOf<DRIVE_LAYOUT_INFORMATION_EX>();
                    int partitionSize = Marshal.SizeOf<PARTITION_INFORMATION_EX>();

                    IntPtr partitionPtr = IntPtr.Add(buffer, headerSize);

                    bool found = false;

                    for (uint i = 0; i < header.PartitionCount; i++)
                    {
                        uint currentPartitionNumber = unchecked((uint)Marshal.ReadInt32(partitionPtr, 24));

                        if (currentPartitionNumber == partitionNumber)
                        {
                            long oldOffset = Marshal.ReadInt64(partitionPtr, 8);
                            long length = Marshal.ReadInt64(partitionPtr, 16);

                            Marshal.WriteInt64(partitionPtr, 8, newStartingOffset);
                            Marshal.WriteByte(partitionPtr, 28, 1);

                            found = true;
                            break;
                        }

                        partitionPtr = IntPtr.Add(partitionPtr, partitionSize);
                    }

                    if (!found)
                        throw new InvalidOperationException($"Partition {partitionNumber} was not found on disk {diskNumber}.");

                    success = DeviceIoControl(handle, IOCTL_DISK_SET_DRIVE_LAYOUT_EX, buffer, bytesReturned, IntPtr.Zero, 0, out _, IntPtr.Zero);

                    if (!success)
                    {
                        int error = Marshal.GetLastWin32Error();
                        throw new Win32Exception(error, $"IOCTL_DISK_SET_DRIVE_LAYOUT_EX failed. Win32 error: {error}");
                    }

                    success = DeviceIoControl(handle, IOCTL_DISK_UPDATE_PROPERTIES, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);

                    if (!success)
                    {
                        int error = Marshal.GetLastWin32Error();
                    }

                    return;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);
    }
}
