namespace JollyDiskPart.ViewModels
{
    public class CleanDiskDialogViewModel : ViewModelBase
    {
        private bool _cleanAll;

        public bool CleanAll
        {
            get => _cleanAll;
            set
            {
                _cleanAll = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Description));
            }
        }

        public string Description => CleanAll ? "Clean All will overwrite every sector on the disk. This may take several hours." : "Clean removes the partition table only. Existing data becomes inaccessible but is not securely erased.";
    }
}
