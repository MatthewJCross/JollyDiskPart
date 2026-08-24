using System.Collections.ObjectModel;

namespace JolyDiskPart.Models
{
    public class DiskModel
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public double Size { get; set; }
        public string Status { get; set; } = "";

        public ObservableCollection<PartitionModel> Partitions { get; } = new();
    }
}
