using JollyDiskPart.ViewModels;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace JollyDiskPart.Controls
{
    /// <summary>
    /// Interaction logic for PartitionResizeBar.xaml
    /// </summary>
    public partial class PartitionResizeBar : UserControl
    {
        private bool dragging;
        private double startX;

        public PartitionResizeBar()
        {
            InitializeComponent();
        }

        private void Handle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            dragging = true;
            startX = e.GetPosition(this).X;
            CaptureMouse();
        }

        private void Handle_MouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging)
                return;

            var current = e.GetPosition(this).X;
            var delta = current - startX;

            if (DataContext is ResizePartitionViewModel vm)
            {
                vm.ChangeSize(delta);
            }

            startX = current;
        }

        private void Handle_MouseUp(object sender, MouseButtonEventArgs e)
        {
            dragging = false;
            ReleaseMouseCapture();
        }
    }
}
