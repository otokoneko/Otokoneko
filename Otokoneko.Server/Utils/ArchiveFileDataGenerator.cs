using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Otokoneko.DataType;
using SharpCompress.Common;
using SharpCompress.Writers;
using SixLabors.ImageSharp;

namespace Otokoneko.Server.Utils
{
    public class RingBufferStream : Stream
    {
        private ArrayPool<byte> ArrayPool => ArrayPool<byte>.Shared;
        private byte[] _buffer;
        private byte[] Buffer
        {
            get => _buffer;
            set
            {
                if(_buffer != null)
                {
                    ArrayPool.Return(_buffer);
                }
                _buffer = value;
            }
        }
        private long ReadBase {  get; set; }
        private long WriteBase {  get; set; }
        private long _length;
        public RingBufferStream(int bufferSize)
        {
            Buffer = ArrayPool.Rent(bufferSize);
        }

        public override bool CanTimeout => false;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _length;
        public override void Flush() { }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] dst, int offset, int count)
        {
            ValidateBufferArgs(dst, offset, count);
            int readBytes = (int)Math.Min(_length, count);
            CopyFromBuffer(dst, offset, readBytes);
            ReadBase = (ReadBase + readBytes) % Buffer.Length;
            _length -= readBytes;
            return readBytes;
        }

        public override void Write(byte[] src, int offset, int count)
        {
            ValidateBufferArgs(src, offset, count);
            if(count + _length > Buffer.Length)
            {
                var newBuffer = ArrayPool.Rent(Math.Max((int)(1.5 * Buffer.Length), (int)(count + _length)));
                CopyFromBuffer(newBuffer, 0, _length);
                Buffer = newBuffer;
                ReadBase = 0;
                WriteBase = _length;
            }
            CopyToBuffer(src, offset, count);
            _length += count;
            WriteBase = (WriteBase + count) % Buffer.Length;
        }

        private void CopyFromBuffer(byte[] dst, long offset, long count)
        {
            Array.Copy(Buffer, ReadBase, dst, offset, Math.Min(count, Buffer.Length - ReadBase));
            if (count > Buffer.Length - ReadBase)
            {
                Array.Copy(Buffer, 0, dst, offset + Buffer.Length - ReadBase, count - (Buffer.Length - ReadBase));
            }
        }

        private void CopyToBuffer(byte[] src, long offset, long count)
        {
            Array.Copy(src, offset, Buffer, WriteBase, Math.Min(count, Buffer.Length - WriteBase));
            if (count > Buffer.Length - WriteBase)
            {
                Array.Copy(src, offset + Buffer.Length - WriteBase, Buffer, 0, count - (Buffer.Length - WriteBase));
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                Buffer = null;
            }
        }

        public override void Close()
        {
            base.Close();
            Buffer = null;
        }

        private static void ValidateBufferArgs(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");
            if (buffer.Length - offset < count)
                throw new ArgumentException("buffer.Length - offset < count");
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
            await using var stream = new RingBufferStream(bufferSize * 2);
            var writer = WriterFactory.Open(stream, ArchiveType.Zip, new WriterOptions(CompressionType.Deflate)
            {
                ArchiveEncoding = new ArchiveEncoding()
                {
                    Default = Encoding.UTF8
                }
            });
            var buffer = new byte[bufferSize];
            for (var i = 0; i < items.Count; i++)
            {
                var (name, node) = items[i];
                await using var readStream = node.OpenRead();
                writer.Write(name, readStream);
                if (i == items.Count - 1) writer.Dispose();
                while (stream.Length >= bufferSize)
                {
                    stream.Read(buffer, 0, bufferSize);
                    yield return buffer;
                }
            }
            var len = stream.Read(buffer, 0, bufferSize);
            yield return buffer.Take(len).ToArray();
        }

        public ArchiveFileDataGenerator(List<Tuple<string, FileTreeNode>> items, int bufferSize = 1024 * 1024)
        {
            _generator = Work(items, bufferSize);
        }
    }
}
