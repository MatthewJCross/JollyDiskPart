using JollyDiskPart.ViewModels;
using JolyDiskPart.Models;
using System.Collections.ObjectModel;

namespace JolyDiskPart.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public ObservableCollection<PartitionModel> Partitions { get; }
        public ObservableCollection<DiskModel> Disks { get; }

        public MainViewModel()
        {
            Partitions = new ObservableCollection<PartitionModel>
            {
                new()
                {
                    Icon="🖴",
                    Name="EFI System Partition",
                    Type="System",
                    FileSystem="FAT32",
                    Label="SYSTEM",
                    Size=272629760,
                    Used="32 MB",
                    Free="228 MB",
                    Status="Healthy"
                },

                new()
                {
                    Icon="🖴",
                    Name="MSR Partition",
                    Type="MSR",
                    FileSystem="—",
                    Label="—",
                    Size=16777216,
                    Used="16 MB",
                    Free="0 MB",
                    Status="Healthy"
                },

                new()
                {
                    Icon="💽",
                    Name="C:",
                    Type="Basic",
                    FileSystem="NTFS",
                    Label="Windows",
                    Size=254863359344,
                    Used="173.12 GB",
                    Free="64.24 GB",
                    Status="Healthy (Boot)"
                },

                new()
                {
                    Icon="💽",
                    Name="D:",
                    Type="Basic",
                    FileSystem="NTFS",
                    Label="Data",
                    Size=509726718689,
                    Used="325.41 GB",
                    Free="356.98 GB",
                    Status="Healthy"
                },

                new()
                {
                    Icon="🛡",
                    Name="Recovery",
                    Type="Recovery",
                    FileSystem="NTFS",
                    Label="Recovery",
                    Size=554696704,
                    Used="452 MB",
                    Free="77 MB",
                    Status="Healthy"
                }
            };

            var disk0 = new DiskModel
            {
                Name = "Disk 0",
                Type = "Basic GPT",
                Size = 931.51 * 1024 * 1024,
                Status = "Online"
            };

            disk0.Partitions.Add(new PartitionModel
            {
                Name = "EFI",
                FileSystem = "FAT32",
                Size = 0.260,
                Colour = "#2F80ED"
            });

            disk0.Partitions.Add(new PartitionModel
            {
                Name = "MSR",
                FileSystem = "",
                Size = 0.016,
                Colour = "#5B8DEF"
            });

            disk0.Partitions.Add(new PartitionModel
            {
                Name = "C: Windows",
                FileSystem = "NTFS",
                Size = 237.36,
                Colour = "#2F80ED"
            });

            disk0.Partitions.Add(new PartitionModel
            {
                Name = "D: Data",
                FileSystem = "NTFS",
                Size = 682.39,
                Colour = "#4CAF50"
            });

            disk0.Partitions.Add(new PartitionModel
            {
                Name = "Recovery",
                FileSystem = "NTFS",
                Size = 0.529,
                Colour = "#9C27B0"
            });

            Disks.Add(disk0);
        }
    }
}
