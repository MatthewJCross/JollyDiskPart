using System.Collections.ObjectModel;
using System.IO;

namespace JollyDiskPart.ViewModels
{
    public class DriveLetterDialogViewModel : ViewModelBase
    {
        public ObservableCollection<string> AvailableLetters { get; } = new() { "" };

        private string? _selectedLetter;
        public string? SelectedLetter
        {
            get => _selectedLetter;
            set => SetField(ref _selectedLetter, value);
        }

        public DriveLetterDialogViewModel(string? currentLetter)
        {
            // Blank = Remove drive letter (already included in initializer)

            var usedLetters = DriveInfo.GetDrives().Select(d => char.ToUpper(d.Name[0])).ToHashSet();
            char? current = null;

            if (!string.IsNullOrWhiteSpace(currentLetter))
                current = char.ToUpper(currentLetter[0]);

            for (char c = 'D'; c <= 'Z'; c++)
            {
                if (!usedLetters.Contains(c) || c == current)
                    AvailableLetters.Add(c.ToString());
            }

            SelectedLetter = current?.ToString() ?? "";
        }
    }
}
