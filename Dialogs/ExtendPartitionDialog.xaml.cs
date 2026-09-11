using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace JollyDiskPart.Dialogs
{
    /// <summary>
    /// Interaction logic for ExtendPartitionDialog.xaml
    /// </summary>
    public partial class ExtendPartitionDialog : Window
    {
        public ExtendPartitionDialog()
        {
            InitializeComponent();
        }

        private void Extend_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
