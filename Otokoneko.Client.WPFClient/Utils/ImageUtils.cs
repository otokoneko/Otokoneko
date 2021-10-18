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
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = memoryStream;
            image.EndInit();
            image.Freeze();
            memoryStream.Close();
            memoryStream.Dispose();
            return image;
        }
    }
}
