namespace JollyDiskPart.ViewModels
{
    public class ShrinkPartitionDialogViewModel : ViewModelBase
    {
        public long MaxShrinkMB { get; }

        private long _sizeMB;
        public long SizeMB
        {
            get => _sizeMB;
            set
            {
                if (value < 0)
                    value = 0;

                if (value > MaxShrinkMB)
                    value = MaxShrinkMB;

                _sizeMB = value;
                OnPropertyChanged();
            }
        }

        public ShrinkPartitionDialogViewModel(long maxShrinkMB)
        {
            MaxShrinkMB = maxShrinkMB;
            SizeMB = maxShrinkMB;
        }
    }
}
