using MessagePack;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Otokoneko.Server.LibraryManage;
using Microsoft.IO;

namespace Otokoneko.DataType
{
    public partial class FileTreeRoot
    {
        [IgnoreMember]
        public IFileTreeNodeRepository Repository { get; set; }
    }

    public partial class FileTreeNode
    {
        [IgnoreMember]
        private static readonly RecyclableMemoryStreamManager Manager = new RecyclableMemoryStreamManager();
        [IgnoreMember]
        private string _extension;
        [IgnoreMember]
        public string Extension => _extension ??= Path.GetExtension(FullName)?.ToLower();
        [IgnoreMember]
        public FileTreeNode Parent { get; set; }
        [IgnoreMember]
        public List<FileTreeNode> Children { get; set; }
        [IgnoreMember]
        public FileTreeRoot Library { get; set; }
        [IgnoreMember]
        public bool IsNewItem { get; set; }

        [IgnoreMember]
        public static FileTreeNode Root => new FileTreeNode()
        {
            ObjectId = 0,
            ParentId = 0,
            FullName = null,
            IsDirectory = true
        };

        private Stream OpenRead(string path)
        {
            if (ObjectId == 0 && IFileTreeRootHandler.Handlers.TryGetValue(Library.Scheme, out var rootHandler))
            {
                var stream = rootHandler.OpenRead(Library, path);
                return stream;
            }

            if (path != null && IFileTreeNodeHandler.Handlers.TryGetValue(Extension, out var nodeHandler))
            {
                var stream = Parent.OpenRead(FullName);
                return nodeHandler.OpenRead(stream, path);
            }

            path = path == null ? FullName : Path.Combine(FullName, path);
            return Parent.OpenRead(path);
        }

        public Stream OpenRead()
        {
            return OpenRead(null);
        }

        private Stream OpenWrite(string path)
        {
            if (ObjectId == 0 && IFileTreeRootHandler.Handlers.TryGetValue(Library.Scheme, out var rootHandler))
            {
                var stream = rootHandler.OpenWrite(Library, path);
                return stream;
            }

            if (path != null && IFileTreeNodeHandler.Handlers.TryGetValue(Extension, out var nodeHandler))
            {
                var stream = Parent.OpenWrite(FullName);
                return nodeHandler.OpenWrite(stream, path);
            }

            path = path == null ? FullName : Path.Combine(FullName, path);
            return Parent.OpenWrite(path);
        }

        public Stream OpenWrite()
        {
            return OpenWrite(null);
        }

        public void Delete()
        {
            Delete(null);
        }

        private void Delete(string path)
        {
            if (ObjectId == 0 && IFileTreeRootHandler.Handlers.TryGetValue(Library.Scheme, out var rootHandler))
            {
                rootHandler.Delete(Library, path);
                return;
            }

            if (path != null && IFileTreeNodeHandler.Handlers.TryGetValue(Extension, out var nodeHandler))
            {
                nodeHandler.Delete(path);
                return;
            }

            path = path == null ? FullName : Path.Combine(FullName, path);
            Parent.Delete(path);
        }

        public async ValueTask<byte[]> ReadAllBytes()
        {
            await using var stream = OpenRead();
            await using var buffer = Manager.GetStream();
            await stream.CopyToAsync(buffer);
            stream.Close();
            return buffer.ToArray();
        }
    }
}
