namespace JollyDiskPart.ViewModels
{
    public class InitializeDiskDialogViewModel : ViewModelBase
    {
        private bool _useGpt = true;
        public bool UseGpt
        {
            get => _useGpt;
            set
            {
                _useGpt = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PartitionStyle));
            }
        }

        public bool UseMbr
        {
            get => !_useGpt;
            set
            {
                _useGpt = !value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PartitionStyle));
            }
        }

        public string PartitionStyle => UseGpt ? "GPT" : "MBR";
    }
}
