using System.IO;
using System.Windows.Media.Imaging;

namespace Otokoneko.Client.WPFClient.Utils
{
    public class ImageUtils
    {
        public static BitmapImage Convert(byte[] imageContent)
        {
            if (imageContent == null) return null;
            var memoryStream = new MemoryStream(imageContent);
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = memoryStream;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
