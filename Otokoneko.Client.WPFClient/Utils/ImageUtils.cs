using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Interop;
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

        public static Bitmap GetBitmap(BitmapSource source)
        {
            var result = new Bitmap(source.PixelWidth, source.PixelHeight, PixelFormat.Format32bppRgb);

            var data = result.LockBits(new Rectangle(Point.Empty, result.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            source.CopyPixels(System.Windows.Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
            result.UnlockBits(data);
            
            return result;
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        public static BitmapSource Merge(BitmapSource up, BitmapSource down)
        {
            using var bmp1 = GetBitmap(up);
            using var bmp2 = GetBitmap(down);

            using var result = new Bitmap(Math.Max(bmp1.Width, bmp2.Width), bmp1.Height + bmp2.Height);

            using var g = Graphics.FromImage(result);

            g.DrawImage(bmp1, Point.Empty);
            g.DrawImage(bmp2, new Point(0, bmp1.Height));

            var hBitmap = result.GetHbitmap();

            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }

        }
    }
}
