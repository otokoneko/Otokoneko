using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Otokoneko.DataType;
using SharpCompress.Common;
using SharpCompress.Writers;
using SixLabors.ImageSharp;

namespace Otokoneko.Server.Utils
{
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
}
