using Newtonsoft.Json;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Filter;
using log4net.Layout;
using Otokoneko.DataType;
using SharpCompress.Common;
using SharpCompress.Writers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using Image = SixLabors.ImageSharp.Image;

namespace Otokoneko.Server
{
    public class AtomicCounter
    {
        private int _current;
        public int Target { get; }

        public AtomicCounter(int current, int target)
        {
            _current = current;
            Target = target;
        }

        public bool IsCompleted()
        {
            return _current == Target;
        }

        public static AtomicCounter operator ++(AtomicCounter counter)
        {
            Interlocked.Increment(ref counter._current);
            return counter;
        }

        public static explicit operator double(AtomicCounter counter)
        {
            return (double)counter._current / counter.Target;
        }
    }

    public static class EncryptUtils
    {
        public static byte[] GenerateRandomBytes(int numberOfBytes)
        {
            using var cryptoProvider = new RNGCryptoServiceProvider();
            var bytes = new byte[numberOfBytes];
            cryptoProvider.GetBytes(bytes);
            return bytes;
        }

        public static string GenerateRandomString(int numberOfBytes)
        {
            using var cryptoProvider = new RNGCryptoServiceProvider();
            var bytes = new byte[numberOfBytes];
            cryptoProvider.GetBytes(bytes);
            var stringBuilder = new StringBuilder();
            foreach (var b in bytes)
            {
                stringBuilder.Append(b.ToString("X2"));
            }
            return stringBuilder.ToString();
        }

        public static bool ComparePassword(string a, string b)
        {
            var res = a.Length ^ b.Length;
            for (var i = 0; i < a.Length && i < b.Length; i++)
            {
                res |= (a[i] ^ b[i]);
            }
            return res == 0;
        }
    }

    public static class CertificateUtils
    {
        public static X509Certificate2 GetCertificate(string path, string password)
        {
            if (!File.Exists(path))
            {
                GenerateCertificate(path, password);
            }
            return new X509Certificate2(path, password);
        }

        public static void GenerateCertificate(string path, string password)
        {
            var req = new CertificateRequest("cn=Otokoneko", RSA.Create(4096), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var cert = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-10), DateTimeOffset.Now.AddYears(10));
            File.WriteAllBytes(path, cert.Export(X509ContentType.Pfx, password));
        }
    }

    public static class ImageUtils
    {
        static ImageUtils()
        {
            Configuration.Default.MemoryAllocator = ArrayPoolMemoryAllocator.CreateWithModeratePooling();
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
            switch (ChooseMode((double)image.Width / image.Height))
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
                    var width = (int)Math.Min(image.Width, (double)image.Height * 0.75);
                    image.Mutate(it => it.Crop(new Rectangle(image.Width - width, 0, (int)width, image.Height)));
                    goto case -1;
                }
                case 2:
                {
                    var width = (int)Math.Min(image.Width, (double)image.Height * 0.75);
                    image.Mutate(it => it.Crop(new Rectangle(0, 0, width, image.Height)));
                    goto case -1;
                    }
                case 3:
                {
                    var width = (int)Math.Min(image.Width, (double)image.Height * 0.75);
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

        public static bool ImageCheck(Stream imageStream, out IImageFormat format)
        {
            try
            {
                using var image = Image.Load(imageStream, out format);
                return true;
            }
            catch
            {
                format = null;
                return false;
            }
        }
    }

    public class ArchiveFileDataGenerator : IAsyncEnumerable<object>
    {
        private readonly IAsyncEnumerable<object> _generator;

        public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken())
        {
            return _generator.GetAsyncEnumerator(cancellationToken);
        }

        private static async IAsyncEnumerable<object> Work(List<Tuple<string, FileTreeNode>> items, int bufferSize)
        {
            await using var memoryStream = new MemoryStream();
            var writer = WriterFactory.Open(memoryStream, ArchiveType.Zip, CompressionType.Deflate);
            for (var i = 0; i < items.Count; i++)
            {
                var (name, node) = items[i];
                await using var readStream = node.OpenRead();
                writer.Write(name, readStream);
                if (i == items.Count - 1) writer.Dispose();
                if (memoryStream.Position < bufferSize) continue;
                int pos;
                for (pos = 0; pos <= memoryStream.Position - bufferSize; pos += bufferSize)
                {
                    var data = new ReadOnlySequence<byte>(memoryStream.GetBuffer(), pos, bufferSize);
                    yield return data;
                }

                if (memoryStream.Position > pos)
                {
                    Array.Copy(memoryStream.GetBuffer(), pos, memoryStream.GetBuffer(), 0, memoryStream.Position - pos);
                }

                memoryStream.Seek(memoryStream.Position - pos, SeekOrigin.Begin);
            }

            yield return new ReadOnlySequence<byte>(memoryStream.GetBuffer(), 0, (int)memoryStream.Position);
        }

        public ArchiveFileDataGenerator(List<Tuple<string, FileTreeNode>> items, int bufferSize = 1024 * 1024)
        {
            _generator = Work(items, bufferSize);
        }
    }

    public static class ServerConfigStringGenerator
    {
        public static string ServerConfigStringGenerate(string host, int port, string certificateHash, string serverName, string serverId)
        {
            var config = new
            {
                Hosts = new List<object>()
                {
                    new
                    {
                        Host = host,
                        Port = port
                    }
                },
                CertificateHash = certificateHash,
                ServerId = serverId,
                ServerName = serverName
            };
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(config));
            return Convert.ToBase64String(bytes);
        }
    }

    public static class DirectoryHelper
    {
        public static void Delete(string path)
        {
            foreach (var file in Directory.GetFiles(path))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (var dir in Directory.GetDirectories(path))
            {
                Delete(dir);
            }

            Directory.Delete(path, false);
        }
    }
}