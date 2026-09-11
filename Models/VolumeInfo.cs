namespace JollyDiskPart.Models
{
    public class VolumeInfo
    {
        public int Number { get; set; }
        public string Name => $"Volume {Number}";
        public string Letter { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Used => FormatBytes(SizeBytes - FreeBytes);
        public string Free => FormatBytes(FreeBytes);
        public string Status { get; set; } = string.Empty;
        public string Info { get; set; } = "";
        public string DriveLetter { get; set; } = string.Empty;
        public string FileSystem { get; set; } = string.Empty;
        public string Size => FormatBytes(SizeBytes);
        public string Type { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public long FreeBytes { get; set; }
        public double SizeGB => SizeBytes / 1024d / 1024d / 1024d;
        public double FreeGB => FreeBytes / 1024d / 1024d / 1024d;
        public long UsedBytes => Math.Max(0, SizeBytes - FreeBytes);

        public double UsedPercent
        {
            get
            {
                if (SizeBytes == 0)
                    return 0;

                return UsedBytes * 100.0 / SizeBytes;
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0)
                return "0 GB";

            if (bytes >= 1024L * 1024 * 1024 * 1024)
                return $"{bytes / (1024d * 1024 * 1024 * 1024):0.##} TB";

            if (bytes >= 1024L * 1024 * 1024)
                return $"{bytes / (1024d * 1024 * 1024):0.##} GB";

            if (bytes >= 1024L * 1024)
                return $"{bytes / (1024d * 1024):0.##} MB";

            return $"{bytes:N0} bytes";
        }

        public bool HasDriveLetter => !string.IsNullOrWhiteSpace(Letter);
        public bool IsOptical => Type.Contains("DVD", StringComparison.OrdinalIgnoreCase) || Type.Contains("CD", StringComparison.OrdinalIgnoreCase);
        public bool IsRemovable => Type.Contains("Removable", StringComparison.OrdinalIgnoreCase);
        public bool Hidden { get; set; }
        public bool ReadOnly { get; set; }
        public bool Boot { get; set; }
        public bool System { get; set; }
        public bool PageFile { get; set; }
        public bool CrashDump { get; set; }
        public bool Hibernation { get; set; }
        public bool BitLocker { get; set; }
        public string Guid { get; set; } = string.Empty;

        public uint SectorsPerCluster { get; set; }
        public uint BytesPerSector { get; set; }
        public long BytesPerCluster => (long)SectorsPerCluster * BytesPerSector;
        public long ClusterSizeKB => BytesPerCluster / 1024;

        public uint FreeClusters { get; set; }
        public uint TotalClusters { get; set; }

        public uint AllocatedClusters => TotalClusters >= FreeClusters ? TotalClusters - FreeClusters : 0;
        public bool HasClusterInfo => SectorsPerCluster > 0 && BytesPerSector > 0 && TotalClusters > 0;
        public string ClusterSizeDisplay => HasClusterInfo ? $"{ClusterSizeKB:N0} KB" : "N/A";
        public string TotalClustersDisplay => HasClusterInfo ? $"{TotalClusters:N0}" : "N/A";
        public string AllocatedClustersDisplay => HasClusterInfo ? $"{AllocatedClusters:N0}" : "N/A";
        public string FreeClustersDisplay => HasClusterInfo ? $"{FreeClusters:N0}" : "N/A";

        public override string ToString()
        {
            if (HasDriveLetter)
                return $"{Letter}: {Label}";

            return Name;
        }
    }
}
