using System.Windows;

namespace JollyDiskPart.ViewModels
{
    /// <summary>
    /// Interaction logic for ConvertDiskDialog.xaml
    /// </summary>
    public partial class ConvertDiskDialog : Window
    {
        public ConvertDiskDialog()
        {
            InitializeComponent();
        }

        private void Convert_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
