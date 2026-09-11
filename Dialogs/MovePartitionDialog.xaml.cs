using System.Windows;
using JollyDiskPart.ViewModels;

namespace JollyDiskPart.Dialogs
{
    /// <summary>
    /// Interaction logic for MovePartitionDialog.xaml
    /// </summary>
    public partial class MovePartitionDialog : Window
    {
        public MovePartitionDialog()
        {
            InitializeComponent();
        }

        private void Move_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MovePartitionDialogViewModel vm && vm.IsValid)
            {
                DialogResult = true;
            }
        }
    }
}
