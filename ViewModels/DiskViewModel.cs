using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.IO;
using JollyDiskPart.Dialogs;
using JollyDiskPart.Models;
using JollyDiskPart.Services;
using JollyDiskPart.Utils;

namespace JollyDiskPart.ViewModels
{
    public class DiskViewModel : ViewModelBase
    {
        private readonly DiskService _diskService;
        private readonly PartitionCorrelator _correlator;
        private readonly PartitionMoveService _partitionMoveService;
        private readonly VolumeMoveService _ntfsMoveService;

        private readonly Dictionary<int, long> _partitionOffsets = new();

        public ObservableCollection<DiskInfo> Disks { get; } = new ObservableCollection<DiskInfo>();

        private DiskInfo? _selectedDisk;
        public DiskInfo? SelectedDisk
        {
            get => _selectedDisk;

            set
            {
                if (SetField(ref _selectedDisk, value))
                {
                    OnPropertyChanged(nameof(SelectedDiskDisplayName));
                    OnPropertyChanged(nameof(CanCreatePartition));
                    OnPropertyChanged(nameof(CanDeletePartition));
                    OnPropertyChanged(nameof(CanFormatPartition));
                    OnPropertyChanged(nameof(CanExtendPartition));
                    OnPropertyChanged(nameof(CanShrinkPartition));
                    OnPropertyChanged(nameof(CanMovePartition));
                    OnPropertyChanged(nameof(CanChangeDriveLetter));
                    OnPropertyChanged(nameof(CanShowProperties));
                    OnPropertyChanged(nameof(CanInitializeDisk));
                    OnPropertyChanged(nameof(CanCleanDisk));
                    OnPropertyChanged(nameof(CanConvertToGpt));
                    OnPropertyChanged(nameof(CanConvertToMbr));
                    OnPropertyChanged(nameof(CanTakeOffline));
                    OnPropertyChanged(nameof(CanBringOnline));

                    //_ = LoadPartitionsAsync(value);
                }
            }
        }

        private PartitionInfo? _selectedPartition;
        public PartitionInfo? SelectedPartition
        {
            get => _selectedPartition;

            set
            {
                if (SetField(ref _selectedPartition, value))
                {
                    OnPropertyChanged(nameof(CanMovePartition));
                    OnPropertyChanged(nameof(CanCreatePartition));
                    OnPropertyChanged(nameof(CanDeletePartition));
                    OnPropertyChanged(nameof(CanFormatPartition));
                }
            }
        }

        public string SelectedDiskDisplayName
        {
            get
            {
                if (SelectedDisk == null)
                    return "Partitions";

                return $"Partitions on {SelectedDisk.Name} ({SelectedDisk.Model})";
            }
        }

        private string? _statusMessage;
        public string? StatusMessage
        {
            get => _statusMessage;

            set => SetField(ref _statusMessage, value);
        }

        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            set => SetField(ref _isScanning, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetField(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(CanPerformDiskOperation));
                }
            }
        }

        private double _progressValue;

        public double ProgressValue
        {
            get => _progressValue;
            set => SetField(ref _progressValue, value);
        }

        public bool CanPerformDiskOperation => !IsBusy;
            
        private DiskInfo? _previewDisk;
        public DiskInfo? PreviewDisk
        {
            get => _previewDisk;
            set
            {
                _previewDisk = value;
                OnPropertyChanged(nameof(PreviewDisk));
            }
        }

        public bool CanCreatePartition => SelectedPartition != null && SelectedPartition.IsUnallocated;
        public bool CanDeletePartition => SelectedPartition != null && !SelectedPartition.IsUnallocated && !SelectedPartition.IsProtected && !SelectedPartition.IsBoot;
        public bool CanFormatPartition => SelectedPartition != null && !SelectedPartition.IsUnallocated && !SelectedPartition.IsProtected && !SelectedPartition.IsBoot;
        public bool CanExtendPartition => SelectedPartition != null && !SelectedPartition.IsUnallocated && !SelectedPartition.IsProtected && SelectedPartition.CanExtend;
        public bool CanShrinkPartition => SelectedDisk != null && SelectedPartition != null && !SelectedPartition.IsUnallocated && !SelectedPartition.IsProtected && SelectedPartition.SizeBytes > 0;
        public bool CanMovePartition => SelectedDisk != null && SelectedPartition != null && !SelectedPartition.IsUnallocated;
        public bool CanChangeDriveLetter => SelectedDisk != null && SelectedPartition != null;
        public bool CanShowProperties => SelectedDisk != null;
        public bool CanInitializeDisk => SelectedDisk != null && !SelectedDisk.IsInitialized;
        public bool CanCleanDisk => SelectedDisk != null && SelectedDisk.SizeBytes <= 0 && !SelectedDisk.IsBoot && !SelectedDisk.IsSystem;
        public bool CanConvertToGpt => SelectedDisk != null && SelectedDisk.PartitionCount < 1 && SelectedDisk.SizeBytes > 0 && !SelectedDisk.IsGPT;
        public bool CanConvertToMbr => SelectedDisk != null && SelectedDisk.PartitionCount < 1 && SelectedDisk.SizeBytes > 0 && !SelectedDisk.IsMBR;
        public bool CanTakeOffline => SelectedDisk != null && !IsBusy && SelectedDisk.IsOnline;
        public bool CanBringOnline => SelectedDisk != null && !IsBusy && !SelectedDisk.IsOnline;

        public ICommand RefreshDisksCommand { get; }
        public ICommand RescanCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand SelectPartitionCommand { get; }
        public ICommand CreatePartitionCommand { get; }
        public ICommand DeletePartitionCommand { get; }
        public ICommand FormatPartitionCommand { get; }
        public ICommand ExtendPartitionCommand { get; }
        public ICommand ShrinkPartitionCommand { get; }
        public ICommand MovePartitionCommand { get; }
        public ICommand ChangeDriveLetterCommand { get; }
        public ICommand ShowPropertiesCommand { get; }
        public ICommand InitialiseDiskCommand { get; }
        public ICommand CleanDiskCommand { get; }
        public ICommand ConvertToGptCommand { get; }
        public ICommand ConvertToMbrCommand { get; }
        public ICommand TakeDiskOfflineCommand { get; }
        public ICommand BringDiskOnlineCommand { get; }

        public DiskViewModel()
        {
            _diskService = new DiskService();
            _correlator = new PartitionCorrelator();
            _ntfsMoveService = new VolumeMoveService();
            _partitionMoveService = new PartitionMoveService(_diskService, _ntfsMoveService);

            RefreshDisksCommand = new RelayCommand(async x => await RefreshSelectedDiskAsync());
            RescanCommand = new RelayCommand(async x => await LoadDisksAsync());
            ExportCommand = new RelayCommand(async x => await ExportInfoAsync());
            SelectPartitionCommand = new RelayCommand(x => { SelectedPartition = x as PartitionInfo; });

            CreatePartitionCommand = new AsyncRelayCommand(CreatePartitionAsync);
            DeletePartitionCommand = new AsyncRelayCommand(DeleteSelectedPartitionAsync);
            FormatPartitionCommand = new AsyncRelayCommand(FormatSelectedPartitionAsync);
            ExtendPartitionCommand = new AsyncRelayCommand(ExtendSelectedPartitionAsync);
            ShrinkPartitionCommand = new AsyncRelayCommand(ShrinkSelectedPartitionAsync);
            MovePartitionCommand = new AsyncRelayCommand(MoveSelectedPartitionAsync);
            ChangeDriveLetterCommand = new AsyncRelayCommand(ChangeDriveLetterAsync);
            ShowPropertiesCommand = new RelayCommand(x => ShowProperties());
            InitialiseDiskCommand = new AsyncRelayCommand(InitializeDiskAsync);
            CleanDiskCommand = new AsyncRelayCommand(CleanDiskAsync);
            ConvertToMbrCommand = new AsyncRelayCommand(() => ConvertDiskAsync(false));
            TakeDiskOfflineCommand = new AsyncRelayCommand(TakeDiskOfflineAsync);
            BringDiskOnlineCommand = new AsyncRelayCommand(BringDiskOnlineAsync);

            _ = LoadDisksAsync();
        }

        public async Task SelectDiskAsync(DiskInfo disk)
        {
            var old = SelectedDisk;

            if (old != null)
                old.IsSelected = false;

            disk.IsSelected = true;
            SelectedDisk = disk;
            await LoadPartitionsAsync(disk);
        }

        private async Task LoadPartitionsAsync(DiskInfo disk)
        {
            var partitions = await BuildPartitionListAsync(disk);
            _diskService.CalculatePartitionWidths(disk, partitions);
            _diskService.ApplyPartitionColours(partitions);
            disk.Partitions.ReplaceAll(partitions);
        }

        private async Task<List<PartitionInfo>> BuildPartitionListAsync(DiskInfo disk)
        {
            var partitions = await _diskService.GetPartitionsAsync(disk.Number, disk.SizeBytes);
            var volumes = await _diskService.GetVolumesAsync();
            return _correlator.Merge(partitions, volumes);
        }

        private async Task LoadDisksAsync()
        {
            try
            {
                IsScanning = true;
                StatusMessage = "Scanning disks...";
                var disks = await _diskService.ScanAsync();
                Disks.Clear();

                foreach (var disk in disks)
                {
                    Disks.Add(disk);
                }

                SelectedDisk = Disks.FirstOrDefault();
                StatusMessage = $"Loaded {Disks.Count} disk(s)";
                IsScanning = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading disks: {ex.Message}";
            }
            finally
            {
                await Task.Delay(1000);   // optional: show completion message briefly
                StatusMessage = "✔ Ready";
            }
        }

        private async Task ExportInfoAsync()
        {
            if (Disks == null || Disks.Count == 0)
            {
                MessageBox.Show("There is no disk information to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Disk Information",
                Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt",
                DefaultExt = ".csv",
                FileName = $"JollyDiskPart_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Disk Number,Disk Name,Disk ID,Status,Type,Partition Style,Size (GB),Free (GB), Partition Number,Partition Name,Partition Type,File System,Drive Letter,Offset (MB),Size (GB)");

                foreach (var disk in Disks)
                {
                    if (disk.Partitions == null || disk.Partitions.Count == 0)
                    {
                        sb.AppendLine(
                            $"{Csv(disk.Number.ToString())}," +
                            $"{Csv(disk.Name)}," +
                            $"{Csv(disk.DiskId)}," +
                            $"{Csv(disk.Status)}," +
                            $"{Csv(disk.Type)}," +
                            $"{Csv(disk.PartitionStyle.ToString())}," +
                            $"{disk.SizeBytes / (1024.0 * 1024 * 1024):F2}," +
                            $"{disk.FreeBytes / (1024.0 * 1024 * 1024):F2}," +
                            ",,,,,,,");

                        continue;
                    }

                    foreach (var partition in disk.Partitions)
                    {
                        sb.AppendLine(
                            $"{Csv(disk.Number.ToString())}," +
                            $"{Csv(disk.Name)}," +
                            $"{Csv(disk.DiskId)}," +
                            $"{Csv(disk.Status)}," +
                            $"{Csv(disk.Type)}," +
                            $"{Csv(disk.PartitionStyle.ToString())}," +
                            $"{disk.SizeBytes / (1024.0 * 1024 * 1024):F2}," +
                            $"{disk.FreeBytes / (1024.0 * 1024 * 1024):F2}," +
                            $"{Csv(partition.Number.ToString())}," +
                            $"{Csv(partition.Name)}," +
                            $"{Csv(disk.PartitionStyle.ToString())}," +
                            $"{Csv(partition.FileSystem)}," +
                            $"{Csv(partition.DriveLetter)}," +
                            $"{partition.SizeBytes / (1024.0 * 1024 * 1024):F2}");
                    }
                }

                await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"Disk information exported successfully.\n\n{dialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export disk information.\n\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string Csv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private async Task RefreshSelectedDiskAsync()
        {
            if (SelectedDisk == null)
                return;

            var partitions = await BuildPartitionListAsync(SelectedDisk);//, offsets);
            
            _diskService.CalculatePartitionWidths(SelectedDisk, partitions);
            SelectedDisk.ReplacePartitions(partitions);
        }

        private async Task CreatePartitionAsync()
        {
            if (SelectedDisk == null || SelectedPartition == null)
                return;

            if (!SelectedPartition.IsUnallocated)
                return;

            // This is the actual physical start of the selected
            // unallocated extent.
            long originalOffset = SelectedPartition.UnallocatedOffsetBytes;

            const long alignment = 1024 * 1024; // 1 MB

            // Align the start UP to the next 1 MB boundary.
            long alignedOffset = ((originalOffset + alignment - 1) / alignment) * alignment;

            // The selected unallocated partition already represents
            // the free extent, so its size is the available extent.
            long freeExtentEnd = originalOffset + SelectedPartition.SizeBytes;

            // Space actually available after alignment.
            long usableBytes = freeExtentEnd - alignedOffset;

            if (usableBytes <= 0)
            {
                MessageBox.Show("There is not enough usable space after applying 1 MB alignment.", "Create Partition", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // DiskPart size is in MB.
            // Always round DOWN.
            long maxSizeMB = usableBytes / (1024 * 1024);
            if (maxSizeMB <= 0)
            {
                MessageBox.Show("There is less than 1 MB of usable space available.", "Create Partition", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            long offsetKB = alignedOffset / 1024;
            var vm = new CreatePartitionDialogViewModel(maxSizeMB);
            var dlg = new CreatePartitionDialog
            {
                Owner = Application.Current.MainWindow,
                DataContext = vm
            };

            if (dlg.ShowDialog() != true)
                return;

            long requestedSizeMB = vm.UseMaximumSpace ? maxSizeMB : vm.SizeMB;
            var result = await _diskService.CreatePartitionAsync(SelectedDisk.Number, vm.SizeMB, offsetKB, vm.FileSystem, vm.Label, string.IsNullOrWhiteSpace(vm.DriveLetter) ? (char?)null : vm.DriveLetter[0], vm.QuickFormat, vm.UseMaximumSpace);

            if (!result.Success)
            {
                MessageBox.Show(result.Error, "Create Partition", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StatusMessage = $"Successfully created partition on disk {SelectedDisk.Number}";

            // Give Windows a moment to update its disk/partition information.
            await Task.Delay(1000);
            await RefreshSelectedDiskAsync();
            StatusMessage = "✔ Ready";

            SelectedPartition = result.Partition;
        }        

        public async Task DeleteSelectedPartitionAsync()
        {
            MessageBoxResult answer;

            if (SelectedDisk == null)
                return;

            if (SelectedPartition == null)
                return;

            answer = MessageBox.Show($"Delete '{SelectedPartition.Name}'?\n\nThis operation cannot be undone.", "Delete Partition", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
                return;

            var result = await _diskService.DeletePartitionAsync(SelectedDisk.Number, SelectedPartition.Number);

            if (!result.Success)
            {
                MessageBox.Show(result.Error, "Delete Partition", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StatusMessage = $"Successfully deleted partition {SelectedPartition.Number} on disk {SelectedDisk.Number}";

            // Give Windows a moment to update its disk/partition information.
            await Task.Delay(1000);
            await RefreshSelectedDiskAsync();
            StatusMessage = "✔ Ready";
        }

        private async Task FormatSelectedPartitionAsync()
        {
            if (SelectedDisk == null || SelectedPartition == null)
                return;

            var vm = new FormatPartitionDialogViewModel(SelectedPartition, "New Volume", "NTFS");

            var dlg = new FormatPartitionDialog
            {
                Owner = Application.Current.MainWindow,
                DataContext = vm
            };

            if (dlg.ShowDialog() != true)
                return;

            var result = await _diskService.FormatPartitionAsync(SelectedDisk.Number, SelectedPartition.Number, vm.FileSystem, vm.Label, vm.QuickFormat);

            if (!result.Success)
            {
                MessageBox.Show(result.Error, "Format Partition", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StatusMessage = $"Successfully formatted partition {SelectedPartition.Number} on disk {SelectedDisk.Number}";

            // Give Windows a moment to update its disk/partition information.
            await Task.Delay(1000);
            await RefreshSelectedDiskAsync();
            StatusMessage = "✔ Ready";
        }

        private async Task ExtendSelectedPartitionAsync()
        {
            if (SelectedDisk == null || SelectedPartition == null || IsBusy)
                return;

            var partitions = SelectedDisk.Partitions.ToList();

            int index = partitions.IndexOf(SelectedPartition);
            if (index < 0 || index >= partitions.Count - 1)
                return;

            var nextPartition = partitions[index + 1];
            if (!nextPartition.IsUnallocated)
            {
                MessageBox.Show("There is no unallocated space immediately after this partition.", "Extend Partition", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            long maxSizeMB = nextPartition.SizeBytes / (1024 * 1024);
            if (maxSizeMB <= 0)
                return;

            var vm = new ExtendPartitionDialogViewModel(maxSizeMB);
            var dlg = new ExtendPartitionDialog
            {
                Owner = Application.Current.MainWindow,
                DataContext = vm
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                IsBusy = true;
                StatusMessage = $"Extending partition {SelectedPartition.Number}...";

                var result = await _diskService.ExtendPartitionAsync(SelectedDisk.Number, SelectedPartition.Number, vm.SizeMB);

                if (!result.Success)
                {
                    MessageBox.Show(result.Error, "Extend Partition", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                StatusMessage = $"Successfully extended partition {SelectedPartition.Number} on disk {SelectedDisk.Number}";

                // Give Windows a moment to update its disk/partition information.
                await Task.Delay(1000);
                await RefreshSelectedDiskAsync();
                StatusMessage = "✔ Ready";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ShrinkSelectedPartitionAsync()
        {
            if (SelectedDisk == null || SelectedPartition == null)
                return;

            long maxShrinkMB = await _diskService.QueryMaximumShrinkAsync(SelectedDisk.Number, SelectedPartition.Number);
            if (maxShrinkMB <= 0)
            {
                MessageBox.Show("This partition cannot be shrunk.", "Shrink Partition", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var vm = new ShrinkPartitionDialogViewModel(maxShrinkMB);
            var dlg = new ShrinkPartitionDialog
            {
                Owner = Application.Current.MainWindow,
                DataContext = vm
            };

            if (dlg.ShowDialog() != true)
                return;

            var result = await _diskService.ShrinkPartitionAsync(SelectedDisk.Number, SelectedPartition.Number, vm.SizeMB);

            if (!result.Success)
            {
                MessageBox.Show(result.Error, "Shrink Partition", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StatusMessage = $"Successfully shrunk partition {SelectedPartition.Number} on disk {SelectedDisk.Number}";

            // Give Windows a moment to update its disk/partition information.
            await Task.Delay(1000);
            await RefreshSelectedDiskAsync();
            StatusMessage = "✔ Ready";
        }

        private async Task MoveSelectedPartitionAsync()
        {
            if (SelectedDisk == null || SelectedPartition == null || IsBusy || !SelectedPartition.CanMove)
                return;

            try
            {
                IsBusy = true;
                StatusMessage = "Reading partition layout...";

                var layout = await _diskService.GetDiskLayoutAsync(SelectedDisk.Number, SelectedDisk.SizeBytes);
                var index = layout.FindIndex(p => p.Partition.Number == SelectedPartition.Number);

                if (index < 0)
                {
                    MessageBox.Show("The selected partition could not be found in the disk layout.", "Move Partition", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var current = layout[index];
                var previous = index > 0 ? layout[index - 1] : null;
                var next = index < layout.Count - 1 ? layout[index + 1] : null;

                bool hasLeftSpace = previous?.Partition.IsUnallocated == true;
                bool hasRightSpace = next?.Partition.IsUnallocated == true;

                if (!hasLeftSpace && !hasRightSpace)
                {
                    MessageBox.Show("There is no unallocated space immediately before or after this partition.", "Move Partition", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                long currentOffsetMB = current.OffsetBytes / (1024 * 1024);
                long minimumOffsetMB = hasLeftSpace ? (previous!.OffsetBytes / (1024 * 1024)) : currentOffsetMB;
                long maximumOffsetMB = hasRightSpace ? ((next!.OffsetBytes + next.Partition.SizeBytes) / (1024 * 1024)) : currentOffsetMB;
                var vm = new MovePartitionDialogViewModel(currentOffsetMB, minimumOffsetMB, maximumOffsetMB);

                IsBusy = false;

                var dlg = new MovePartitionDialog
                {
                    Owner = Application.Current.MainWindow,
                    DataContext = vm
                };

                if (dlg.ShowDialog() != true)
                    return;

                long targetOffsetBytes = vm.NewOffsetMB * 1024L * 1024L;
                bool movingLeft = targetOffsetBytes < current.OffsetBytes;
                var adjacent = movingLeft ? previous : next;

                if (adjacent == null || !adjacent.Partition.IsUnallocated)
                {
                    MessageBox.Show(movingLeft ? "There is no unallocated space immediately before this partition." : "There is no unallocated space immediately after this partition.", "Move Partition", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                StatusMessage = $"Creating move plan for partition {SelectedPartition.Number}...";

                var planner = new PartitionMovePlanner();
                var plan = planner.CreatePlan(current, adjacent, targetOffsetBytes);

                if (!plan.IsValid)
                {
                    MessageBox.Show(plan.Error, "Move Partition", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                StatusMessage = "Move plan created.";

                if (!string.IsNullOrWhiteSpace(current.Partition.DriveLetter))
                {
                    string fileSystem = current.Partition.FileSystem?.Trim().ToUpperInvariant() ?? string.Empty;
                    if (fileSystem == "NTFS")
                    {
                        var preparation = _ntfsMoveService.PrepareMove(current.Partition.DriveLetter);
                        if (!preparation.IsValid)
                        {
                            MessageBox.Show(preparation.Error, "Move Partition", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    else if (fileSystem == "FAT32")
                    {
                        // FAT32 does not use NTFS bitmap preparation.
                        // The actual lock/dismount is handled by MoveAsync().
                    }
                }

                StatusMessage = $"Preparing to move partition {SelectedPartition.Number}...";

                ProgressValue = 0;

                var progress = new Progress<double>(percent =>
                {
                    ProgressValue = percent;
                    StatusMessage = vm.VerifyAfterCopy ? $"Partition copy/verify: {percent:F1}%" : $"Partition copy: {percent:F1}%";
                });

                var moveResult = await _partitionMoveService.MoveAsync(current, adjacent, targetOffsetBytes, vm.VerifyAfterCopy, progress);

                if (!moveResult.Success)
                {
                    MessageBox.Show(moveResult.Error, "Move Partition", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                ProgressValue = 100;
                StatusMessage = "Refreshing disk layout...";

                await LoadDisksAsync();
                StatusMessage = vm.VerifyAfterCopy ? "✔ Raw partition copy and verification completed" : "✔ Raw partition copy completed";
                await Task.Delay(1000);
                StatusMessage = "✔ Ready";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ChangeDriveLetterAsync()
        {
            if (SelectedDisk == null || SelectedPartition == null)
                return;

            var vm = new DriveLetterDialogViewModel(SelectedPartition.DriveLetter);
            var dlg = new DriveLetterDialog
            {
                Owner = Application.Current.MainWindow,
                DataContext = vm
            };

            if (dlg.ShowDialog() != true)
                return;

            char? letter = string.IsNullOrWhiteSpace(vm.SelectedLetter) ? (char?)null : vm.SelectedLetter[0];
            var result = await _diskService.ChangeDriveLetterAsync(SelectedDisk.Number, SelectedPartition.Number, SelectedPartition.DriveLetter, letter);

            if (!result.Success)
            {
                MessageBox.Show(result.Error, "Drive Letter", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StatusMessage = $"Successfully changed drive letter to {letter} of partition {SelectedPartition.Number} on disk {SelectedDisk.Number}";

            // Give Windows a moment to update its disk/partition information.
            await Task.Delay(1000);
            await RefreshSelectedDiskAsync();
            StatusMessage = "✔ Ready";
        }

        private void ShowProperties()
        {
            if (SelectedPartition == null)
                return;

            var dlg = new PropertiesDialog
            {
                Owner = Application.Current.MainWindow,
                DataContext = new PropertiesDialogViewModel(SelectedPartition)
            };

            dlg.ShowDialog();
        }

        private async Task InitializeDiskAsync()
        {
            if (SelectedDisk == null)
                return;

            if (SelectedDisk.IsInitialized)
            {
                MessageBox.Show("This disk has already been initialized.", "Initialize Disk", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var vm = new InitializeDiskDialogViewModel();

            var dlg = new InitializeDiskDialog
            {
                Owner = Application.Current.MainWindow,
                DataContext = vm
            };

            if (dlg.ShowDialog() != true)
                return;

            var result = await _diskService.InitializeDiskAsync(SelectedDisk.Number, vm.UseGpt);

            if (!result.Success)
            {
                MessageBox.Show(result.Error, "Initialize Disk", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StatusMessage = $"Successfully initialized disk {SelectedDisk.Number}";

            // Give Windows a moment to update its disk/partition information.
            await Task.Delay(1000);
            await RefreshSelectedDiskAsync();
            StatusMessage = "✔ Ready";
        }

        private async Task CleanDiskAsync()
        {
            if (SelectedDisk == null)
                return;

            var vm = new CleanDiskDialogViewModel();

            var dlg = new CleanDiskDialog
            {
                Owner = Application.Current.MainWindow,
                DataContext = vm
            };

            if (dlg.ShowDialog() != true)
                return;

            var message = vm.CleanAll ? "Clean All will overwrite the entire disk and cannot be undone. Continue?" : "Cleaning the disk will remove all partitions. Continue?";

            if (MessageBox.Show(message, "Confirm Clean Disk", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            int diskNumber = SelectedDisk.Number;
            StatusMessage = $"Cleaning disk {diskNumber}...";
            var result = await _diskService.CleanDiskAsync(diskNumber, vm.CleanAll);

            if (!result.Success)
            {
                MessageBox.Show(result.Error, "Clean Disk", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StatusMessage = $"Successfully cleaned disk {diskNumber}";

            // Give Windows a moment to update its disk/partition information.
            await Task.Delay(1000);
            await RefreshSelectedDiskAsync();
            StatusMessage = "✔ Ready";
        }

        private async Task ConvertDiskAsync(bool useGpt)
        {
            if (SelectedDisk == null)
                return;

            if (SelectedDisk.Partitions.Any())
            {
                MessageBox.Show("The disk must contain no partitions before it can be converted.", "Convert Disk", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var vm = new ConvertDiskDialogViewModel(useGpt ? "Convert to GPT" : "Convert to MBR", useGpt ? "The selected disk will be converted to the GPT partition style." : "The selected disk will be converted to the MBR partition style.", "Convert");
            var dlg = new ConvertDiskDialog
            {
                Owner = Application.Current.MainWindow,
                DataContext = vm
            };

            if (dlg.ShowDialog() != true)
                return;

            var result = await _diskService.ConvertDiskAsync(SelectedDisk.Number, useGpt);
            if (!result.Success)
            {
                MessageBox.Show(result.Error, "Convert Disk", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StatusMessage = $"Successfully converted disk {SelectedDisk.Number} to {(useGpt ? "GPT" : "MBR")}";

            // Give Windows a moment to update its disk/partition information.
            await Task.Delay(1000);
            await RefreshSelectedDiskAsync();
            StatusMessage = "✔ Ready";
        }

        private async Task TakeDiskOfflineAsync()
        {
            if (SelectedDisk == null || IsBusy)
                return;

            try
            {
                IsBusy = true;
                StatusMessage = $"Taking Disk {SelectedDisk.Number} offline...";

                var result = await _diskService.TakeOfflineAsync(SelectedDisk.Number);

                if (!result.Success)
                {
                    MessageBox.Show(result.Error, "Disk Offline", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                SelectedDisk.IsOnline = false;
                OnPropertyChanged(nameof(CanTakeOffline));
                OnPropertyChanged(nameof(CanBringOnline));
                StatusMessage = $"✔ Disk {SelectedDisk.Number} is offline";
                await Task.Delay(1000);
                StatusMessage = "✔ Ready";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task BringDiskOnlineAsync()
        {
            if (SelectedDisk == null || IsBusy)
                return;

            try
            {
                IsBusy = true;
                StatusMessage = $"Bringing Disk {SelectedDisk.Number} online...";

                var result = await _diskService.BringOnlineAsync(SelectedDisk.Number);

                if (!result.Success)
                {
                    MessageBox.Show(result.Error, "Disk Online", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                SelectedDisk.IsOnline = true;
                OnPropertyChanged(nameof(CanTakeOffline));
                OnPropertyChanged(nameof(CanBringOnline));
                StatusMessage = $"✔ Disk {SelectedDisk.Number} is online";
                await Task.Delay(1000);
                StatusMessage = "✔ Ready";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
