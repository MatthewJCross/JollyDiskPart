using System.Globalization;
using System.Windows.Data;

namespace JolyDiskPart.Converters
{
    public class PartitionWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 3)
                return 50d;

            if (values[0] is not double partition)
                return 50d;

            if (values[1] is not double disk)
                return 50d;

            if (values[2] is not double totalWidth)
                return 50d;

            if (disk == 0)
                return 50d;

            var w = totalWidth * (partition / disk);

            return Math.Max(90, w);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
