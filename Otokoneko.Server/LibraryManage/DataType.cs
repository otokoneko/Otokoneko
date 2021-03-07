using MessagePack;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Otokoneko.Server.LibraryManage;

namespace Otokoneko.DataType
{
    public partial class FileTreeRoot
    {
        [IgnoreMember]
        public IFileTreeNodeRepository Repository { get; set; }
    }

    public partial class FileTreeNode
    {
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

            path = path == null ? FullName : Path.Combine(FullName, path);
            return Parent.OpenWrite(path);
        }

        public Stream OpenWrite()
        {
            return OpenWrite(null);
        }

        public async ValueTask<byte[]> ReadAllBytes()
        {
            await using var stream = OpenRead();
            await using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            stream.Close();
            return memoryStream.ToArray();
        }
    }

    // public partial class LibraryPathItem
    // {
    //     private static readonly IdGenerator IdGenerator = new IdGenerator(0);
    //
    //     public LibraryPathItem()
    //     {
    //         Id = IdGenerator.CreateId();
    //         StructType = FileStructType.None;
    //     }
    //
    //     [SerializationConstructor]
    //     public LibraryPathItem(long id)
    //     {
    //         Id = id;
    //     }
    //
    //     [IgnoreMember]
    //     public string Name =>
    //         PathType == LibraryPathType.Directory ? FullName.Split(Path.DirectorySeparatorChar).Last() : Path.GetFileNameWithoutExtension(FullName);
    //     
    //     [IgnoreMember]
    //     public string Extension => Path.GetExtension(FullName);
    //     
    //     [IgnoreMember]
    //     public LibraryPathItem Parent { get; set; }
    //     
    //     [IgnoreMember]
    //     public List<LibraryPathItem> Children { get; set; }
    //
    //     [IgnoreMember]
    //     public bool IsNewItem { get; set; }
    //
    //     [Key(5)]
    //     public string Password { get; set; }
    //
    //     [IgnoreMember] 
    //     public bool Visible { get; set; } = true;
    //
    //     public async ValueTask<byte[]> ReadAllBytes()
    //     {
    //         await using var stream = OpenRead();
    //         await using var memoryStream = new MemoryStream();
    //         await stream.CopyToAsync(memoryStream);
    //         stream.Close();
    //         return memoryStream.ToArray();
    //     }
    //
    //     private Stream OpenRead(string path)
    //     {
    //         var name = Uri.UnescapeDataString(FullName);
    //         var password = Password != null ? Uri.UnescapeDataString(Password) : null;
    //         if (PathType == LibraryPathType.CompressedFile && path != null)
    //         {
    //             var handler = new ArchiveFileHandler();
    //             var result = Parent.OpenRead(name);
    //             return handler.OpenRead(result, path, password);
    //         }
    //         path = path == null ? name : Path.Combine(name, path);
    //         if (ParentId == 0)
    //         {
    //             var handler = new FileSystemHandler();
    //             return handler.OpenRead(null, path, password);
    //         }
    //         else
    //         {
    //             return Parent.OpenRead(path);
    //         }
    //     }
    //
    //     public Stream OpenRead()
    //     {
    //         if (PathType == LibraryPathType.Directory) return null;
    //         return OpenRead(null);
    //     }
    //
    //     public Stream OpenWrite()
    //     {
    //         var path = FullName;
    //         var parent = Parent;
    //         while (parent.Id!=0)
    //         {
    //             path = Path.Combine(parent.FullName, path);
    //             parent = parent.Parent;
    //         }
    //         return File.OpenWrite(path);
    //     }
    // }
}
