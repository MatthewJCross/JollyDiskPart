using System.Windows;

namespace JollyDiskPart.Dialogs
{
    /// <summary>
    /// Interaction logic for FormatPartitionDialog.xaml
    /// </summary>
    public partial class FormatPartitionDialog : Window
    {
        public FormatPartitionDialog()
        {
            InitializeComponent();
        }

        private void Format_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
