using System.Windows;

namespace JollyDiskPart.Dialogs
{
    /// <summary>
    /// Interaction logic for ShrinkPartitionDialog.xaml
    /// </summary>
    public partial class ShrinkPartitionDialog : Window
    {
        public ShrinkPartitionDialog()
        {
            InitializeComponent();
        }

        private void Shrink_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
