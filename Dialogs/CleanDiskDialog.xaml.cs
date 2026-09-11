using System.Windows;

namespace JollyDiskPart.Dialogs
{
    /// <summary>
    /// Interaction logic for CleanDiskDialog.xaml
    /// </summary>
    public partial class CleanDiskDialog : Window
    {
        public CleanDiskDialog()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
