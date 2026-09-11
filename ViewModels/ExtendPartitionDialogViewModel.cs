namespace JollyDiskPart.ViewModels
{
    public class ExtendPartitionDialogViewModel : ViewModelBase
    {
        public long MaxSizeMB { get; }

        private long _sizeMB;
        public long SizeMB
        {
            get => _sizeMB;
            set
            {
                if (value > MaxSizeMB)
                    value = MaxSizeMB;

                _sizeMB = value;
                OnPropertyChanged();
            }
        }

        public ExtendPartitionDialogViewModel(long maxSizeMB)
        {
            MaxSizeMB = maxSizeMB;

            // Default: use all available space
            SizeMB = maxSizeMB;
        }
    }
}
