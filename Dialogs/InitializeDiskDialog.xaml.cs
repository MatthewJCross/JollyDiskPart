using System.Windows;

namespace JollyDiskPart.Dialogs
{
    /// <summary>
    /// Interaction logic for InitializeDiskDialog.xaml
    /// </summary>
    public partial class InitializeDiskDialog : Window
    {
        public InitializeDiskDialog()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
