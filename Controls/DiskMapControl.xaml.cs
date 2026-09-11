using JollyDiskPart.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JollyDiskPart.Controls
{
    /// <summary>
    /// Interaction logic for DiskMapControl.xaml
    /// </summary>
    public partial class DiskMapControl : UserControl
    {
        public event EventHandler<DiskInfo>? DiskSelectionRequested;

        public DiskMapControl()
        {
            InitializeComponent();
            Loaded += DiskMapControl_Loaded;
            Unloaded += DiskMapControl_Unloaded;
        }

        private void DiskMapControl_Loaded(object sender, RoutedEventArgs e)
        {
            AttachToDisk(Disk);
            BuildPartitionMap();
        }

        private void AttachToDisk(DiskInfo? disk)
        {
            if (disk == null)
                return;

            disk.Partitions.CollectionChanged -= Partitions_CollectionChanged;
            disk.Partitions.CollectionChanged += Partitions_CollectionChanged;
        }

        private void DetachFromDisk(DiskInfo? disk)
        {
            if (disk == null)
                return;

            disk.Partitions.CollectionChanged -= Partitions_CollectionChanged;
        }

        private void DiskMapControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (Disk != null)
                Disk.Partitions.CollectionChanged -= Partitions_CollectionChanged;
        }

        public ObservableCollection<PartitionInfo>? PreviewPartitions
        {
            get => (ObservableCollection<PartitionInfo>)GetValue(PreviewPartitionsProperty);
            set => SetValue(PreviewPartitionsProperty, value);
        }

        public static readonly DependencyProperty PreviewPartitionsProperty = DependencyProperty.Register(nameof(PreviewPartitions), typeof(ObservableCollection<PartitionInfo>), typeof(DiskMapControl), new PropertyMetadata(null, PreviewPartitionsChanged));

        public DiskInfo Disk
        {
            get => (DiskInfo)GetValue(DiskProperty);
            set => SetValue(DiskProperty, value);
        }

        public static readonly DependencyProperty DiskProperty = DependencyProperty.Register(nameof(Disk), typeof(DiskInfo), typeof(DiskMapControl), new PropertyMetadata(null, DiskChanged));
        private static void PreviewPartitionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DiskMapControl control)
                control.BuildPartitionMap();
        }

        private static void DiskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DiskMapControl control)
                return;

            control.DetachFromDisk(e.OldValue as DiskInfo);
            control.AttachToDisk(e.NewValue as DiskInfo);
        }

        private void Partitions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            BuildPartitionMap();
        }

        public PartitionInfo SelectedPartition
        {
            get => (PartitionInfo)GetValue(SelectedPartitionProperty);
            set => SetValue(SelectedPartitionProperty, value);
        }

        public static readonly DependencyProperty SelectedPartitionProperty = DependencyProperty.Register(nameof(SelectedPartition), typeof(PartitionInfo), typeof(DiskMapControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, SelectedPartitionChanged));

        public DiskInfo SelectedDisk
        {
            get => (DiskInfo)GetValue(SelectedDiskProperty);
            set => SetValue(SelectedDiskProperty, value);
        }

        public static readonly DependencyProperty SelectedDiskProperty = DependencyProperty.Register(nameof(SelectedDisk), typeof(DiskInfo), typeof(DiskMapControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        private void BuildPartitionMap()
        {
            var sw = Stopwatch.StartNew();

            PartitionGrid.Children.Clear();
            PartitionGrid.ColumnDefinitions.Clear();

            var disk = Disk;

            if (disk == null)
                return;

            var partitions = disk.Partitions;

            if (partitions == null || partitions.Count == 0)
                return;

            DrawPartitions(partitions);
        }

        private void DrawPartitions(IEnumerable<PartitionInfo> partitions)
        {
            foreach (var partition in partitions)
            {
                bool isUnallocated = partition.IsUnallocated;

                PartitionGrid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(partition.DisplayWidth, GridUnitType.Pixel)
                });

                var border = new Border
                {
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(5),
                    Background = new SolidColorBrush(Color.FromRgb(35, 35, 35)),
                    Style = (Style)FindResource("PartitionBorderStyle"),
                    Tag = partition
                };

                // Only real partitions are selectable
                if (!isUnallocated)
                    border.MouseLeftButtonDown += Partition_MouseLeftButtonDown;

                var stack = new StackPanel
                {
                    Margin = new Thickness(8)
                };

                stack.Children.Add(new TextBlock
                {
                    Text = isUnallocated ? "Unallocated" : partition.Name,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold
                });

                stack.Children.Add(new TextBlock
                {
                    Text = partition.SizeDisplay,
                    Foreground = Brushes.LightGray,
                    FontSize = 11
                });

                if (!isUnallocated)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = partition.FileSystem,
                        Foreground = Brushes.LightGray,
                        FontSize = 11
                    });
                }

                var grid = new Grid();

                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = new GridLength(8)
                });

                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = GridLength.Auto
                });

                var colourStrip = new Border
                {
                    Background = isUnallocated ? Brushes.DimGray : partition.Colour,
                    CornerRadius = new CornerRadius(5, 5, 0, 0)
                };

                Grid.SetRow(colourStrip, 0);
                Grid.SetRow(stack, 1);

                grid.Children.Add(colourStrip);
                grid.Children.Add(stack);

                border.Child = grid;

                Grid.SetColumn(border, PartitionGrid.ColumnDefinitions.Count - 1);

                PartitionGrid.Children.Add(border);
            }
        }        

        private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Disk == null)
                return;

            DiskSelectionRequested?.Invoke(this, Disk);
            e.Handled = true;
        }

        private void Partition_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is PartitionInfo partition)
            {
                if (Disk?.Partitions != null)
                {
                    foreach (var p in Disk.Partitions)
                    {
                        p.IsSelected = false;
                    }
                }

                partition.IsSelected = true;
                SelectedPartition = partition;
            }

            e.Handled = true;
        }

        private static void SelectedPartitionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DiskMapControl control)
                return;

            if (e.OldValue is PartitionInfo oldPartition)
                oldPartition.IsSelected = false;

            if (e.NewValue is PartitionInfo newPartition)
                newPartition.IsSelected = true;
        }
    }
}

