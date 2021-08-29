using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Otokoneko.Client.WPFClient.Model
{
    public static class Utils
    {
        private static readonly string[] SizeSuffixes = { "KB", "MB", "GB" };
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

        public static string FormatLocalDateTime(DateTime? localDateTime)
        {
            if (localDateTime == null) return string.Empty;
            var time = (DateTime)localDateTime;
            return
                $"{time.Year:D4}.{time.Month:D2}.{time.Day:D2} {time.Hour:D2}:{time.Minute:D2}";
        }

        public static string FormatSizeOfBytes(double size)
        {
            foreach (var suffix in SizeSuffixes)
            {
                size /= 1024;
                if (size < 1024)
                {
                    return $"{size:F} {suffix}";
                }
            }
            return $"{size:F} {SizeSuffixes.Last()}";
        }
    }
}
