using System.ComponentModel;

namespace JolyDiskPart.Models
{
    public class PartitionModel
    {
        public string Icon { get; set; } = "💽";

        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string FileSystem { get; set; } = "";
        public string Label { get; set; } = "";

        public double Size { get; set; }
        public string SizeGB => $"{Size:0.##} GB";

        public string Used { get; set; } = "";
        public string Free { get; set; } = "";
        public string Status { get; set; } = "";

        public string Colour { get; set; } = "#2F80ED";

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
