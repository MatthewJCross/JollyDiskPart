using System.Windows;
using JollyDiskPart.ViewModels;

namespace JollyDiskPart.Dialogs
{
    /// <summary>
    /// Interaction logic for CreatePartitionDialog.xaml
    /// </summary>
    public partial class CreatePartitionDialog : Window
    {
        public CreatePartitionDialog()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is CreatePartitionDialogViewModel oldVm)
                oldVm.CloseRequested -= Vm_CloseRequested;

            if (e.NewValue is CreatePartitionDialogViewModel newVm)
                newVm.CloseRequested += Vm_CloseRequested;
        }

        private void Vm_CloseRequested(bool? result)
        {
            DialogResult = result;
        }
    }
}
