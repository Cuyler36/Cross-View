using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Cross_View
{
    internal static class Util
    {
        public static BitmapSource ToImage(in byte[] array, int width, int height) =>
            BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, array, width * 4);
    }
}
