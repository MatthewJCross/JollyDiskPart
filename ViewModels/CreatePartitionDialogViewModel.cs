using System.Collections.ObjectModel;
using System.IO;
using JollyDiskPart.Utils;

namespace JollyDiskPart.ViewModels
{
    public class CreatePartitionDialogViewModel : ViewModelBase
    {
        private string _label = "New Volume";
        private string _fileSystem = "NTFS";
        private string _driveLetter = "D";
        private long _sizeMB;
        private long _maxSizeMB;
        private bool _quickFormat = true;
        private bool _isPrimary = true;
        private string _clusterSize = "Default";
        private bool _useMaximumSpace;

        //public event PropertyChangedEventHandler? PropertyChanged;

        public event Action<bool?>? CloseRequested;

        public AsyncRelayCommand CreateCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand UseMaximumSpaceCommand { get; }

        public CreatePartitionDialogViewModel(long maxSizeMB)
        {
            CreateCommand = new AsyncRelayCommand(CreateAsync, CanCreate);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            UseMaximumSpaceCommand = new AsyncRelayCommand(SetMaximumSpace);

            MaxSizeMB = Math.Max(0, maxSizeMB);
            SizeMB = MaxSizeMB;
            _useMaximumSpace = true;

            FileSystems = new ObservableCollection<string>
            {
                "NTFS",
                "FAT32",
                "FAT",
                "exFAT"
            };

            FileSystem = FileSystems[0];

            ClusterSizes = new ObservableCollection<string>
            {
                "Default",
                "512",
                "1024",
                "2048",
                "4096",
                "8192",
                "16384",
                "32768",
                "65536"
            };

            var used = DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet();

            AvailableDriveLetters = new ObservableCollection<string>();

            for (char c = 'D'; c <= 'Z'; c++)
            {
                if (!used.Contains(c))
                    AvailableDriveLetters.Add(c.ToString());
            }

            if (AvailableDriveLetters.Count > 0)
                DriveLetter = AvailableDriveLetters[0];
        }

        private Task SetMaximumSpace()
        {
            UseMaximumSpace = true;
            SizeMB = MaxSizeMB;

            return Task.CompletedTask;
        }

        public ObservableCollection<string> FileSystems { get; }

        public ObservableCollection<string> ClusterSizes { get; }

        public ObservableCollection<string> AvailableDriveLetters { get; }

        public string Label
        {
            get => _label;
            set
            {
                if (SetField(ref _label, value))
                    CreateCommand.RaiseCanExecuteChanged();
            }
        }

        public string FileSystem
        {
            get => _fileSystem;
            set => SetField(ref _fileSystem, value);
        }

        public string ClusterSize
        {
            get => _clusterSize;
            set => SetField(ref _clusterSize, value);
        }

        public string DriveLetter
        {
            get => _driveLetter;
            set => SetField(ref _driveLetter, value);
        }

        public bool QuickFormat
        {
            get => _quickFormat;
            set => SetField(ref _quickFormat, value);
        }

        public bool IsPrimary
        {
            get => _isPrimary;
            set => SetField(ref _isPrimary, value);
        }

        public bool UseMaximumSpace
        {
            get => _useMaximumSpace;
            set
            {
                if (SetField(ref _useMaximumSpace, value))
                {
                    OnPropertyChanged(nameof(SizeDisplay));
                    CreateCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public long SizeMB
        {
            get => _sizeMB;
            set
            {
                var newValue = value;

                if (newValue < 1)
                    newValue = 1;

                if (newValue > MaxSizeMB)
                    newValue = MaxSizeMB;

                if (SetField(ref _sizeMB, newValue))
                {
                    // If the user manually changes the size,
                    // it is no longer explicitly "Use Maximum".
                    if (newValue != MaxSizeMB && _useMaximumSpace)
                    {
                        _useMaximumSpace = false;
                        OnPropertyChanged(nameof(UseMaximumSpace));
                    }

                    OnPropertyChanged(nameof(SizeGB));
                    OnPropertyChanged(nameof(SizeDisplay));

                    CreateCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public long MaxSizeMB
        {
            get => _maxSizeMB;
            set
            {
                if (SetField(ref _maxSizeMB, Math.Max(0, value)))
                {
                    if (_sizeMB > _maxSizeMB)
                        _sizeMB = _maxSizeMB;

                    OnPropertyChanged(nameof(SizeGB));
                    OnPropertyChanged(nameof(MaxSizeGB));
                    OnPropertyChanged(nameof(SizeDisplay));

                    CreateCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public double SizeGB =>
            Math.Round(SizeMB / 1024.0, 2);

        public double MaxSizeGB =>
            Math.Round(MaxSizeMB / 1024.0, 2);

        public string SizeDisplay =>
            $"{SizeGB:N2} GB of {MaxSizeGB:N2} GB";

        private Task CreateAsync()
        {
            CloseRequested?.Invoke(true);
            return Task.CompletedTask;
        }

        private Task CancelAsync()
        {
            CloseRequested?.Invoke(false);
            return Task.CompletedTask;
        }

        private bool CanCreate()
        {
            return SizeMB > 0 &&
                   SizeMB <= MaxSizeMB &&
                   !string.IsNullOrWhiteSpace(Label);
        }
    }
}
