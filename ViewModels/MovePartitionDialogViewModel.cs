namespace JollyDiskPart.ViewModels
{
    public class MovePartitionDialogViewModel : ViewModelBase
    {
        private long _currentOffsetMB;
        public long CurrentOffsetMB
        {
            get => _currentOffsetMB;
            set => SetField(ref _currentOffsetMB, value);
        }

        private long _newOffsetMB;
        public long NewOffsetMB
        {
            get => _newOffsetMB;
            set => SetField(ref _newOffsetMB, value);
        }

        private long _minimumOffsetMB;
        public long MinimumOffsetMB
        {
            get => _minimumOffsetMB;
            set => SetField(ref _minimumOffsetMB, value);
        }

        private long _maximumOffsetMB;
        public long MaximumOffsetMB
        {
            get => _maximumOffsetMB;
            set => SetField(ref _maximumOffsetMB, value);
        }

        private bool _verifyAfterCopy = false;
        public bool VerifyAfterCopy
        {
            get => _verifyAfterCopy;
            set => SetField(ref _verifyAfterCopy, value);
        }

        public string CurrentPositionDisplay => $"{CurrentOffsetMB:N0} MB";
        public string NewPositionDisplay => $"{NewOffsetMB:N0} MB";
        public string RangeDisplay => $"{MinimumOffsetMB:N0} MB - {MaximumOffsetMB:N0} MB";

        public MovePartitionDialogViewModel(long currentOffsetMB, long minimumOffsetMB, long maximumOffsetMB)
        {
            CurrentOffsetMB = currentOffsetMB;
            NewOffsetMB = currentOffsetMB;
            MinimumOffsetMB = minimumOffsetMB;
            MaximumOffsetMB = maximumOffsetMB;
        }

        public bool IsValid => NewOffsetMB >= MinimumOffsetMB && NewOffsetMB <= MaximumOffsetMB;
    }
}
