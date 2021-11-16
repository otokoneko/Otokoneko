using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using WebP.Net;

namespace Otokoneko.Server.Utils
{
    public static class ImageUtils
    {
        public sealed class WebpFormat : IImageFormat
        {
            public static WebpFormat Instance
            {
                get;
            } = new WebpFormat();

            public string Name => "WEBP";

            public string DefaultMimeType => "image/webp";

            public IEnumerable<string> MimeTypes => new string[] { "image/webp" };

            public IEnumerable<string> FileExtensions => new string[] { "webp" };

            private WebpFormat()
            {
            }
        }

        private static System.Timers.Timer ReleaseMemoryTimer;

        static ImageUtils()
        {
            Configuration.Default.MemoryAllocator = ArrayPoolMemoryAllocator.CreateWithModeratePooling();
            ReleaseMemoryTimer = new System.Timers.Timer
            {
                AutoReset = false,
                Enabled = true
            };
            ReleaseMemoryTimer.Elapsed += (s, e) =>
            {
                ReleaseMemory();
            };
        }

        private static readonly List<double> ZoomModeThreshold = new List<double>()
        {
            0.75,
            1,
            1.5,
            2,
        };

        private static int ChooseMode(double ratio)
        {
            var index = -1;
            var value = double.MaxValue;
            for (var i = 0; i < ZoomModeThreshold.Count; i++)
            {
                var tmp = Math.Abs(ZoomModeThreshold[i] - ratio);
                if (!(tmp < value)) continue;
                index = i;
                value = tmp;
            }

            return index;
        }

        public static void ZoomImage(Stream inStream, Stream outStream, int maxHeight, int maxWidth)
        {
            using var image = Image.Load(inStream);

            var mode = ChooseMode((double)image.Width / image.Height);

            if (mode > 0)
            {
                var rw = (double)maxWidth / image.Width;
                var rh = (double)maxHeight / image.Height;
                var r = Math.Max(rw, rh);
                if(r <= 1)
                {
                    var width = (int)(image.Width * r);
                    var height = (int)(image.Height * r);
                    image.Mutate(it => it.Resize(width, height));
                }
            }

            switch (mode)
            {
                case -1:
                    {
                        var rw = (double)maxWidth / image.Width;
                        var rh = (double)maxHeight / image.Height;
                        var r = Math.Min(rw, rh);
                        var width = (int)(image.Width * r);
                        var height = (int)(image.Height * r);
                        image.Mutate(it => it.Resize(width, height));
                        break;
                    }
                case 0:
                    {
                        goto case -1;
                    }
                case 1:
                    {
                        var width = (int)Math.Min(image.Width, image.Height * 0.75);
                        image.Mutate(it => it.Crop(new Rectangle(image.Width - width, 0, width, image.Height)));
                        goto case -1;
                    }
                case 2:
                    {
                        var width = (int)Math.Min(image.Width, image.Height * 0.75);
                        image.Mutate(it => it.Crop(new Rectangle(0, 0, width, image.Height)));
                        goto case -1;
                    }
                case 3:
                    {
                        var width = (int)Math.Min(image.Width, image.Height * 0.75);
                        var x = Math.Max(0, image.Width / 2 - width);
                        image.Mutate(it => it.Crop(new Rectangle(x, 0, width, image.Height)));
                        goto case -1;
                    }
            }
            image.Save(outStream, new JpegEncoder());
        }

        public static async ValueTask<Tuple<bool, int, int>> GetMetadata(Stream imageStream)
        {
            try
            {
                var imageInfo = await Image.IdentifyAsync(imageStream);
                return new Tuple<bool, int, int>(true, imageInfo.Width, imageInfo.Height);
            }
            catch
            {
                return new Tuple<bool, int, int>(false, -1, -1);
            }
        }

        public static bool ImageCheck(Stream imageStream, long length, out IImageFormat format)
        {
            try
            {
                imageStream.Seek(0, SeekOrigin.Begin);
                using var image = Image.Load(imageStream, out format);
                return true;
            }
            catch
            {
                format = null;
            }
            try
            {
                var cache = new byte[length];
                imageStream.Seek(0, SeekOrigin.Begin);
                imageStream.Read(cache, 0, (int)length);
                using var webp = new WebPObject(cache);
                using var image = webp.GetImage();
                if (image != null)
                {
                    format = WebpFormat.Instance;
                    return true;
                }
            }
            catch
            {
                format = null;
            }
            return false;
        }

        public static void ReleaseMemory()
        {
            Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
        }

        public static void ReleaseMemory(TimeSpan delayTime)
        {
            ReleaseMemoryTimer.Interval = delayTime.TotalMilliseconds;
        }
    }
}
