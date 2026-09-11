using System.Windows.Media;
using JollyDiskPart.ViewModels;

namespace JollyDiskPart.Models
{
    public class PartitionInfo : ViewModelBase
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        private int _number;
        public int Number
        {
            get => _number;
            set => SetField(ref _number, value);
        }

        private string _type = string.Empty;
        public string Type
        {
            get => _type;
            set => SetField(ref _type, value);
        }

        /// <summary>
        /// GPT type GUID or MBR type.
        /// </summary>
        private string _typeId = string.Empty;
        public string TypeId
        {
            get => _typeId;
            set => SetField(ref _typeId, value);
        }

        private PartitionStyle _partitionType;
        public PartitionStyle PartitionType
        {
            get => _partitionType;
            set => SetField(ref _partitionType, value);
        }

        private string _size = string.Empty;
        public string Size
        {
            get => _size;
            set => SetField(ref _size, value);
        }

        private long _sizeBytes;
        public long SizeBytes
        {
            get => _sizeBytes;
            set => SetField(ref _sizeBytes, value);
        }

        public long SizeMB => SizeBytes / (1024 * 1024);

        public string FileSystemDisplay => string.IsNullOrWhiteSpace(FileSystem) ? "Unknown" : FileSystem;
        private string _fileSystem = string.Empty;
        public string FileSystem
        {
            get => _fileSystem;
            set
            {
                if (SetField(ref _fileSystem, value))
                {
                    OnPropertyChanged(nameof(FileSystemDisplay));
                    OnPropertyChanged(nameof(UsedDisplay));
                    OnPropertyChanged(nameof(FreeDisplay));
                }
            }
        }

        public string LabelDisplay => string.IsNullOrWhiteSpace(Label) ? "---" : Label; 
        private string _label = string.Empty;
        public string Label
        {
            get => _label;
            set => SetField(ref _label, value);
        }

        public string DriveLetterDisplay => string.IsNullOrWhiteSpace(DriveLetter) ? string.Empty : $"{DriveLetter}:";
        private string _driveLetter = string.Empty;
        public string DriveLetter
        {
            get => _driveLetter;
            set => SetField(ref _driveLetter, value);
        }

        private string _status = string.Empty;
        public string Status
        {
            get => _status;
            set => SetField(ref _status, value);
        }

        public string UsedDisplay => string.IsNullOrWhiteSpace(FileSystem) ? "N/A" : string.IsNullOrWhiteSpace(Used) ? "N/A" : Used;
        private string _used = string.Empty;
        public string Used
        {
            get => _used;
            set => SetField(ref _used, value);
        }

        public string FreeDisplay => string.IsNullOrWhiteSpace(FileSystem) ? "N/A" : string.IsNullOrWhiteSpace(Free) ? "N/A" : Free;
        private string _free = string.Empty;
        public string Free
        {
            get => _free;
            set => SetField(ref _free, value);
        }

        private int? _volumeNumber;
        public int? VolumeNumber
        {
            get => _volumeNumber;
            set => SetField(ref _volumeNumber, value);
        }

        private bool _hidden;
        public bool Hidden
        {
            get => _hidden;
            set => SetField(ref _hidden, value);
        }

        private bool _required;
        public bool Required
        {
            get => _required;
            set => SetField(ref _required, value);
        }

        private bool _readOnly;
        public bool ReadOnly
        {
            get => _readOnly;
            set => SetField(ref _readOnly, value);
        }

        private bool _isUnallocated;
        public bool IsUnallocated 
        { 
            get => _isUnallocated; 
            set => SetField(ref _isUnallocated, value); 
        }

        // ===== Partition Classification =====
        public bool IsEFI => TypeId.Equals("c12a7328-f81f-11d2-ba4b-00a0c93ec93b", StringComparison.OrdinalIgnoreCase);
        public bool IsMicrosoftReserved => TypeId.Equals("e3c9e316-0b5c-4db8-817d-f92df00215ae", StringComparison.OrdinalIgnoreCase);
        public bool IsBasicData => TypeId.Equals("ebd0a0a2-b9e5-4433-87c0-68b6b72699c7", StringComparison.OrdinalIgnoreCase);
        public bool IsRecovery => TypeId.Equals("de94bba4-06d1-4d40-a16a-bfd50179d6ac", StringComparison.OrdinalIgnoreCase);
        public bool IsOEM => Type.Contains("OEM", StringComparison.OrdinalIgnoreCase);
        public bool IsSystem => IsEFI || IsMicrosoftReserved;
        public bool IsBoot => DriveLetter.Equals("C", StringComparison.OrdinalIgnoreCase);
        public bool IsProtected => IsEFI || IsMicrosoftReserved || IsRecovery || IsOEM;
        public bool CanDelete => !IsBoot;
        public bool CanFormat => !IsEFI && !IsMicrosoftReserved;
        public bool CanAssignLetter => !IsEFI && !IsMicrosoftReserved;
        public bool IsAllocated => !IsUnallocated;

        // ===== UI =====
        private Brush _partitionBrush = Brushes.SteelBlue;
        public Brush PartitionBrush
        {
            get => _partitionBrush;
            set => SetField(ref _partitionBrush, value);
        }

        private Brush? _colour;
        public Brush? Colour
        {
            get => _colour;
            set => SetField(ref _colour, value);
        }

        public string PartitionIcon
        {
            get
            {
                string file = PartitionType switch
                {
                    PartitionStyle.Basic => "partition.png",
                    PartitionStyle.EFI => "efi.png",
                    PartitionStyle.MSR => "msr.png",
                    PartitionStyle.Recovery => "recovery.png",
                    PartitionStyle.Dynamic => "dynamic.png",
                    PartitionStyle.GPT => "gpt.png",
                    PartitionStyle.RAW => "raw.png",
                    _ => "unknown.png"
                };

                return $"/JollyDiskPart;component/Resources/{file}";
            }
        }

        private string _icon = "💽";
        public string Icon
        {
            get => _icon;
            set => SetField(ref _icon, value);
        }

        private double _displayWidth = 80;
        public double DisplayWidth
        {
            get => _displayWidth;
            set => SetField(ref _displayWidth, value);
        }

        public string SizeDisplay
        {
            get => Size;
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public bool CanResize { get; set; }
        public bool CanExtend { get; set; }
        public bool IsSystemPartition { get; set; }
        public bool IsHiddenPartition { get; set; }
        public Guid PartitionTypeGuid { get; set; }

        private long _unallocatedOffsetBytes;
        public long UnallocatedOffsetBytes
        {
            get => _unallocatedOffsetBytes;
            set => SetField(ref _unallocatedOffsetBytes, value);
        }

        private bool _canMove;
        public bool CanMove
        {
            get => _canMove;
            set => SetField(ref _canMove, value);
        }

        private long? _clusterSizeKB;
        public long? ClusterSizeKB
        {
            get => _clusterSizeKB;
            set => SetField(ref _clusterSizeKB, value);
        }

        private long? _totalClusters;
        public long? TotalClusters
        {
            get => _totalClusters;
            set => SetField(ref _totalClusters, value);
        }

        private long? _allocatedClusters;
        public long? AllocatedClusters
        {
            get => _allocatedClusters;
            set => SetField(ref _allocatedClusters, value);
        }

        private long? _freeClusters;
        public long? FreeClusters
        {
            get => _freeClusters;
            set => SetField(ref _freeClusters, value);
        }


        public PartitionInfo Clone()
        {
            return new PartitionInfo
            {
                Number = Number,
                SizeBytes = SizeBytes,
                FileSystem = FileSystem,
                Label = Label,
                DriveLetter = DriveLetter,
                Type = Type,
                VolumeNumber = VolumeNumber
            };
        }
    }
}
