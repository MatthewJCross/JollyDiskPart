using System.Linq;
using System.Collections.ObjectModel;
using System.Windows.Media;
using JollyDiskPart.ViewModels;

namespace JollyDiskPart.Models
{
    public class DiskInfo :ViewModelBase
    {
        public int Number { get; set; }
        public string? Name { get; set; } = string.Empty;
        public string? DiskId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Type { get; set; } = string.Empty;
        public double Size { get; set; }
        public string? SizeGB { get; set; } 
        public long SizeBytes { get; set; }
        public long FreeBytes { get; set; }
        public bool IsGPT => PartitionStyle == PartitionStyle.GPT;
        public bool IsMBR => PartitionStyle == PartitionStyle.MBR;
        public bool IsRAW => PartitionStyle == PartitionStyle.RAW;
        public bool IsUSB => BusType == BusType.USB;
        public bool IsSSD => MediaType == MediaType.SSD;
        public bool IsHDD => MediaType == MediaType.HDD;
        public bool IsOptical => MediaType == MediaType.Optical;
        private bool _isOnline;
        public bool IsOnline
        {
            get => _isOnline;
            set => SetField(ref _isOnline, value);
        }

        public bool IsReadOnly { get; set; }
        public bool IsBoot { get; set; }
        public bool IsSystem { get; set; }
        public bool IsRemovable { get; set; }
        public bool IsDynamic { get; set; }
        public string DiskGuid { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string InterfaceType { get; set; } = string.Empty;
        public int SectorSize { get; set; }

        private readonly BulkObservableCollection<PartitionInfo> _partitions = new();
        public BulkObservableCollection<PartitionInfo> Partitions => _partitions;

        public int PartitionCount => Partitions.Count;
        public long UsedBytes => Partitions.Sum(p => p.SizeBytes);
        public long UnallocatedBytes => SizeBytes - UsedBytes;

        public double UsedPercent
        {
            get
            {
                if (SizeBytes == 0)
                    return 0;

                return UsedBytes * 100.0 / SizeBytes;
            }
        }

        public double FreePercent
        {
            get
            {
                if (SizeBytes == 0)
                    return 0;

                return UnallocatedBytes * 100.0 / SizeBytes;
            }
        }

        public override string ToString()
        {
            return Name ?? string.Empty;
        }

        public bool ReadOnly { get; set; }
        public bool CurrentReadOnly { get; set; }
        public bool BootDisk { get; set; }
        public bool PageFileDisk { get; set; }
        public bool CrashDumpDisk { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public PartitionStyle PartitionStyle { get; set; }
        public DiskType DiskType { get; set; }
        public BusType BusType { get; set; }
        public MediaType MediaType { get; set; }

        public string PartitionStyleText => IsUninitialized ? "Uninitialised" : PartitionStyle.ToString();
        public string DiskTypeText => DiskType.ToString();
        public string BusTypeText => BusType.ToString();

        public ImageSource? Icon { get; set; }

        private bool _isUninitialized;
        public bool IsUninitialized
        {
            get => _isUninitialized;
            set
            {
                if (SetField(ref _isUninitialized, value))
                {
                    OnPropertyChanged(nameof(IsInitialized));
                    OnPropertyChanged(nameof(PartitionStyleText));
                }
            }
        }

        public bool IsInitialized => !IsUninitialized;

        public string PnpDeviceId { get; set; } = "";
        public string FirmwareRevision { get; set; } = "";

        public bool IsUsb { get; set; }
        public bool IsNvme { get; set; }

        public DiskInfo Clone()
        {
            var clone = new DiskInfo
            {
                Number = Number,
                Model = Model,
                SizeBytes = SizeBytes
            };

            foreach (var partition in Partitions)
            {
                clone.Partitions.Add(partition.Clone());
            }

            return clone;
        }

        public void ReplacePartitions(IEnumerable<PartitionInfo> partitions)
        {
            Partitions.Clear();

            foreach (var partition in partitions)
                Partitions.Add(partition);

            OnPropertyChanged(nameof(Partitions));
            OnPropertyChanged(nameof(PartitionCount));
            OnPropertyChanged(nameof(UsedBytes));
            OnPropertyChanged(nameof(UnallocatedBytes));
            OnPropertyChanged(nameof(UsedPercent));
            OnPropertyChanged(nameof(FreePercent));
        }
    }
}
