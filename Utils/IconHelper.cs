using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JollyDiskPart.Utils
{
    public static class IconHelper
    {
        public static ImageSource Load(string relativePath)
        {
            var uri = new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute);

            return new BitmapImage(uri);
        }
    }
}
