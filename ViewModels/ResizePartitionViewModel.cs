using System.Collections.ObjectModel;
using JollyDiskPart.Models;

namespace JollyDiskPart.ViewModels
{
    public class ResizePartitionViewModel : ViewModelBase
    {
        private long _newSizeMB;
        public PartitionInfo Partition { get; }
        public long OriginalSizeMB { get; }
        public long MinimumSizeMB { get; }
        public long MaximumSizeMB { get; }

        public long NewSizeMB
        {
            get => _newSizeMB;
            set
            {
                if (SetField(ref _newSizeMB, value))
                {
                    OnPropertyChanged(nameof(UsedWidth));
                    OnPropertyChanged(nameof(FreeWidth));
                }
            }
        }

        public double UsedWidth
        {
            get
            {
                return
                (double)NewSizeMB /
                OriginalSizeMB *
                500;
            }
        }

        public double FreeWidth
        {
            get
            {
                return
                500 - UsedWidth;
            }
        }

        private DiskInfo? _selectedDisk;
        public DiskInfo? SelectedDisk
        {
            get => _selectedDisk;
            set
            {
                _selectedDisk = value;
                OnPropertyChanged(nameof(SelectedDisk));
            }
        }
        
        public ResizePartitionViewModel(PartitionInfo partition)
        {
            Partition = partition;
            OriginalSizeMB = partition.SizeMB;
            MinimumSizeMB = 1024;
            MaximumSizeMB = partition.SizeMB;
            NewSizeMB = partition.SizeMB;
        }

        public void ChangeSize(double pixels)
        {
            const double MB_PER_PIXEL = 100;

            var change = (long)(pixels * MB_PER_PIXEL);
            var newSize = NewSizeMB + change;

            if (newSize < MinimumSizeMB)
                newSize = MinimumSizeMB;

            if (newSize > MaximumSizeMB)
                newSize = MaximumSizeMB;

            NewSizeMB = newSize;
        }
    }
}
