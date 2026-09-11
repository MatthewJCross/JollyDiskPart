using JollyDiskPart.Models;

namespace JollyDiskPart.ViewModels
{
    public class FormatPartitionDialogViewModel : ViewModelBase
    {
        public PartitionInfo Partition { get; }
        public string FileSystem { get; set; } = "NTFS";
        public string Label { get; set; } = "";
        public bool QuickFormat { get; set; } = true;

        public FormatPartitionDialogViewModel(PartitionInfo partition, string label, string fileSystem)
        {
            Partition = partition;
            Label = label;
            FileSystem = string.IsNullOrWhiteSpace(partition.FileSystem) ? "NTFS" : fileSystem;
        }
    }
}
