using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace JollyDiskPart.Interop
{
    internal static class VolumeNative
    {
        private const uint FILE_DEVICE_FILE_SYSTEM = 0x00000009;

        private const uint METHOD_BUFFERED = 0;
        private const uint METHOD_NEITHER = 3;

        private const uint FILE_ANY_ACCESS = 0;

        internal const uint GenericRead = 0x80000000;
        internal const uint GenericWrite = 0x40000000;

        internal const uint FileShareRead = 0x00000001;
        internal const uint FileShareWrite = 0x00000002;

        internal static readonly uint FsctlGetVolumeBitmap = FsctlCode(27, METHOD_NEITHER);
        internal const uint OpenExisting = 3; private static uint FsctlCode(uint function, uint method, uint access = FILE_ANY_ACCESS)
        {
            return (FILE_DEVICE_FILE_SYSTEM << 16) | (access << 14) | (function << 2) | method;
        }

        public static readonly uint FSCTL_LOCK_VOLUME = FsctlCode(6, METHOD_BUFFERED);
        public static readonly uint FSCTL_UNLOCK_VOLUME = FsctlCode(7, METHOD_BUFFERED);
        public static readonly uint FSCTL_DISMOUNT_VOLUME = FsctlCode(8, METHOD_BUFFERED);
        public static readonly uint FSCTL_MOVE_FILE = FsctlCode(29, METHOD_BUFFERED);
        public static readonly uint FSCTL_GET_VOLUME_BITMAP = FsctlCode(27, METHOD_NEITHER);
        public static readonly uint FSCTL_GET_RETRIEVAL_POINTERS = FsctlCode(28, METHOD_NEITHER);
        internal static readonly uint FSCTL_GET_NTFS_VOLUME_DATA = FsctlCode(25, METHOD_BUFFERED);

        [StructLayout(LayoutKind.Sequential)]
        public struct MOVE_FILE_DATA
        {
            public long FileHandle;
            public long StartingVcn;
            public long StartingLcn;
            public uint ClusterCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTING_LCN_INPUT_BUFFER
        {
            public long StartingLcn;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTING_VCN_INPUT_BUFFER
        {
            public long StartingVcn;
        }

        internal struct StartingLcnInputBuffer
        {
            public long StartingLcn;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RetrievalPointersBuffer
        {
            public long StartingVcn;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MoveFileData
        {
            public long FileHandle;
            public long StartingVcn;
            public long StartingLcn;
            public uint ClusterCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NTFS_VOLUME_DATA_BUFFER
        {
            internal long VolumeSerialNumber;
            internal long NumberSectors;
            internal long TotalClusters;
            internal long FreeClusters;
            internal long TotalReserved;
            internal uint BytesPerSector;
            internal uint BytesPerCluster;
            internal uint BytesPerFileRecordSegment;
            internal uint ClustersPerFileRecordSegment;
            internal long MftValidDataLength;
            internal long MftStartLcn;
            internal long Mft2StartLcn;
            internal long MftZoneStart;
            internal long MftZoneEnd;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool GetDiskFreeSpace(string lpRootPathName, out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters, out uint lpTotalNumberOfClusters);
    }
}
