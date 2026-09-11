namespace JollyDiskPart.ViewModels
{
    public class ConvertDiskDialogViewModel : ViewModelBase
    {
        public string Title { get; }

        public string Message { get; }

        public string ButtonText { get; }

        public ConvertDiskDialogViewModel(string title, string message, string buttonText)
        {
            Title = title;
            Message = message;
            ButtonText = buttonText;
        }
    }
}
