using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IdGen;
using Otokoneko.DataType;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Otokoneko.Server.LibraryManage
{
    public interface IFileTreeRootHandler
    {
        public static Dictionary<string, IFileTreeRootHandler> Handlers { get; } = new Dictionary<string, IFileTreeRootHandler>();
        public HashSet<string> SupportSchemes { get; }

        public bool IsLegal(FileTreeRoot library);

        public Stream OpenWrite(FileTreeRoot library, string path);
        public Stream OpenRead(FileTreeRoot library, string path);

        public bool CreateFileTree(FileTreeNode root, IdGenerator idGenerator);
    }

    public interface IFileTreeNodeHandler
    {
        public static Dictionary<string, IFileTreeNodeHandler> Handlers { get; } = new Dictionary<string, IFileTreeNodeHandler>();
        public HashSet<string> SupportExtensions { get; }

        public Stream OpenRead(Stream input, string path);

        public bool CreateFileTree(Stream input, FileTreeNode root, IdGenerator idGenerator);
    }

    public class FileSystemHandler : IFileTreeRootHandler
    {
        public HashSet<string> SupportSchemes => new HashSet<string>()
        {
            "file"
        };

        public bool IsLegal(FileTreeRoot library)
        {
            return Directory.Exists(library.Path);
        }

        public Stream OpenWrite(FileTreeRoot library, string path)
        {
            return File.OpenWrite(Path.Combine(library.Path, path));
        }

        public Stream OpenRead(FileTreeRoot library, string path)
        {
            return File.OpenRead(Path.Combine(library.Path, path));
        }

        private void CreateFileTree(string path, FileTreeNode root, IdGenerator idGenerator)
        {
            var dirInfo = new DirectoryInfo(path);
            root.FullName ??= dirInfo.Name;
            root.Children ??= new List<FileTreeNode>();

            foreach (var directoryInfo in dirInfo.GetDirectories())
            {
                var child = root.Children.FirstOrDefault(it => it.FullName == directoryInfo.Name) ??
                            new FileTreeNode
                            {
                                IsNewItem = true,
                                IsDirectory = true,
                                ObjectId = idGenerator.CreateId(),
                                Library = root.Library
                            };

                CreateFileTree(
                    directoryInfo.FullName,
                    child,
                    idGenerator);
                if (!child.IsNewItem) continue;
                child.ParentId = root.ObjectId;
                child.Parent = root;
                root.Children.Add(child);
            }

            foreach (var fileInfo in dirInfo.GetFiles())
            {
                var child = root.Children.FirstOrDefault(it => it.FullName == fileInfo.Name) ??
                            new FileTreeNode
                            {
                                FullName = fileInfo.Name,
                                IsNewItem = true,
                                IsDirectory = false,
                                ObjectId = idGenerator.CreateId(),
                                Library = root.Library
                            };

                if (!child.IsNewItem) continue;
                child.ParentId = root.ObjectId;
                child.Parent = root;
                root.Children.Add(child);
            }
        }

        public bool CreateFileTree(FileTreeNode root, IdGenerator idGenerator)
        {
            CreateFileTree(root.Library.Path, root, idGenerator);
            return true;
        }

        public static void Register()
        {
            var instance = new FileSystemHandler();
            foreach (var scheme in instance.SupportSchemes)
            {
                IFileTreeRootHandler.Handlers.Add(scheme, instance);
            }
        }
    }

    public class ArchiveStream : Stream
    {
        public override void Flush()
        {
            _data.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _data.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _data.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _data.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _data.Write(buffer, offset, count);
        }

        private readonly Stream _data;
        private readonly IArchive _archive;

        public ArchiveStream(Stream data, IArchive archive)
        {
            _data = data;
            _archive = archive;
        }

        public override bool CanRead => _data.CanRead;
        public override bool CanSeek => _data.CanSeek;
        public override bool CanWrite => _data.CanWrite;
        public override long Length => _data.Length;

        public override long Position
        {
            get => _data.Position;
            set => _data.Position = value;
        }

        public override async ValueTask DisposeAsync()
        {
            _archive.Dispose();
            await _data.DisposeAsync();
        }

        public override void Close()
        {
            DisposeAsync();
        }
    }

    public class ArchiveFileHandler : IFileTreeNodeHandler
    {
        public HashSet<string> SupportExtensions => new HashSet<string>()
        {
            ".zip",
            ".rar",
            ".7z",
            "tar",
            ".gz"
        };

        public bool CreateFileTree(Stream input, FileTreeNode root, IdGenerator idGenerator)
        {
            var options = new ReaderOptions { LookForHeader = true, LeaveStreamOpen = false };
            using var archive = ArchiveFactory.Open(input, options);

            root.Children = new List<FileTreeNode>();

            var pathToItem = new Dictionary<string, FileTreeNode>();
            var nameToItem = new Dictionary<string, FileTreeNode> { { "", root } };
            foreach (var entry in archive.Entries)
            {
                var path = entry.Key;
                var item = new FileTreeNode
                {
                    ObjectId = idGenerator.CreateId(),
                    IsNewItem = true,
                    FullName = Path.GetFileName(path.Last() == '/' ? path.Substring(0, path.Length - 1) : path),
                    IsDirectory = entry.IsDirectory,
                    Library = root.Library
                };
                pathToItem.Add(path, item);
                if (!entry.IsDirectory) continue;
                item.Children = new List<FileTreeNode>();
                nameToItem.Add(Path.GetDirectoryName(path + (archive.Type == ArchiveType.Rar ? "/" : "")), item);
            }

            foreach (var (path, child) in pathToItem)
            {
                var parent = nameToItem[Path.GetDirectoryName(path.TrimEnd('\\').TrimEnd('/'))];
                child.ParentId = parent.ObjectId;
                child.Parent = parent;
                parent.Children.Add(child);
            }

            if (root.Children.Count == 1 && root.Children.First().IsDirectory)
            {
                var child = root.Children.First();
                root.Children = new List<FileTreeNode>();
                foreach (var grandChild in child.Children)
                {
                    grandChild.FullName = Path.Combine(child.FullName, grandChild.FullName);
                    grandChild.ParentId = root.ObjectId;
                    grandChild.Parent = root;
                    root.Children.Add(grandChild);
                }
            }

            return true;
        }

        public Stream OpenRead(Stream input, string path)
        {
            var options = new ReaderOptions { LookForHeader = true, LeaveStreamOpen = false };
            var archive = ArchiveFactory.Open(input, options);
            path = archive.Type != ArchiveType.Rar ? path.Replace('\\', '/') : path.Replace('/', '\\');
            var entry = archive.Entries.Single(e => e.Key == path);
            var stream = entry.OpenEntryStream();
            return new ArchiveStream(stream, archive);
        }

        public static void Register()
        {
            var instance = new ArchiveFileHandler();
            foreach (var extension in instance.SupportExtensions)
            {
                IFileTreeNodeHandler.Handlers.Add(extension, instance);
            }
        }
    }

    // public interface ILibraryHandler
    // {
    //     public Stream OpenWrite(Stream input, string path, string authentication);
    //     public Stream OpenRead(Stream input, string path, string authentication);
    //     public bool List(LibraryPathItem root, Stream input, string path, string authentication, List<LibraryPathItem> specialItems);
    //
    //     private static readonly HashSet<string> CompressedFileExtensions =
    //         new HashSet<string>()
    //         {
    //             ".zip",
    //             ".rar",
    //             ".7z",
    //             "tar",
    //             ".gz"
    //         };
    //
    //     private static readonly HashSet<string> ImageFileExtensions = new HashSet<string>
    //     {
    //         ".jpg",
    //         ".jpeg",
    //         ".png",
    //         ".bmp",
    //         ".gif",
    //         ".webp"
    //     };
    //
    //     protected static readonly HashSet<LibraryPathType> SpecialPathTypes = new HashSet<LibraryPathType>()
    //     {
    //         LibraryPathType.CompressedFile
    //     };
    //
    //     protected static LibraryPathType ExtensionToLibraryPathType(string extension)
    //     {
    //         extension = extension.ToLower();
    //         if (CompressedFileExtensions.Contains(extension))
    //         {
    //             return LibraryPathType.CompressedFile;
    //         }
    //         else if (ImageFileExtensions.Contains(extension))
    //         {
    //             return LibraryPathType.ImageFile;
    //         }
    //         else
    //         {
    //             return LibraryPathType.NormalFile;
    //         }
    //     }
    // }

    // public class FileSystemHandler : ILibraryHandler
    // {
    //     public Stream OpenWrite(Stream input, string path, string authentication)
    //     {
    //         return File.OpenWrite(path);
    //     }
    //
    //     public Stream OpenRead(Stream input, string path, string authentication)
    //     {
    //         return File.OpenRead(path);
    //     }
    //
    //     private LibraryPathItem List(string path, bool isFile, LibraryPathItem root, List<LibraryPathItem> specialItems)
    //     {
    //         root ??= new LibraryPathItem { IsNewItem = true };
    //
    //         if (isFile)
    //         {
    //             root.FullName = Path.GetFileName(path);
    //             root.PathType = ILibraryHandler.ExtensionToLibraryPathType(root.Extension);
    //             if (ILibraryHandler.SpecialPathTypes.Contains(root.PathType))
    //             {
    //                 specialItems.Add(root);
    //             }
    //         }
    //         else
    //         {
    //             if (File.Exists(Path.Combine(path, ".ignore")))
    //             {
    //                 return null;
    //             }
    //
    //             var dirInfo = new DirectoryInfo(path);
    //             root.FullName ??= dirInfo.Name;
    //             root.PathType = LibraryPathType.Directory;
    //             root.Children ??= new List<LibraryPathItem>();
    //
    //             foreach (var directoryInfo in dirInfo.GetDirectories())
    //             {
    //                 var child = List(
    //                     directoryInfo.FullName, 
    //                     false, 
    //                     root.Children.FirstOrDefault(it => it.FullName == directoryInfo.Name), 
    //                     specialItems);
    //                 if (child == null || !child.IsNewItem) continue;
    //                 child.ParentId = root.Id;
    //                 child.Parent = root;
    //                 root.Children.Add(child);
    //             }
    //
    //             foreach (var fileInfo in dirInfo.GetFiles())
    //             {
    //                 var child = List(
    //                     fileInfo.FullName, 
    //                     true, 
    //                     root.Children.FirstOrDefault(it => it.FullName == fileInfo.Name), 
    //                     specialItems);
    //                 if (child == null || !child.IsNewItem) continue;
    //                 child.ParentId = root.Id;
    //                 child.Parent = root;
    //                 root.Children.Add(child);
    //             }
    //         }
    //
    //         return root;
    //     }
    //
    //     public bool List(LibraryPathItem root, Stream input, string path, string authentication, List<LibraryPathItem> specialItems)
    //     {
    //         if (!Directory.Exists(path))
    //         {
    //             return false;
    //         }
    //
    //         List(path, false, root, specialItems);
    //
    //         return true;
    //     }
    // }

    // public class ArchiveFileHandler : ILibraryHandler
    // {
    //     public Stream OpenWrite(Stream input, string path, string authentication)
    //     {
    //         throw new NotImplementedException();
    //     }
    //
    //     public Stream OpenRead(Stream input, string path, string authentication)
    //     {
    //         var options = new ReaderOptions { LookForHeader = true, LeaveStreamOpen = false, Password = authentication };
    //         var archive = ArchiveFactory.Open(input, options);
    //         path = archive.Type != ArchiveType.Rar ? path.Replace('\\', '/') : path.Replace('/', '\\');
    //         var entry = archive.Entries.Single(e => e.Key == path);
    //         return entry.OpenEntryStream();
    //     }
    //
    //     public bool List(LibraryPathItem root, Stream input, string _, string authentication, List<LibraryPathItem> specialItems)
    //     {
    //         var options = new ReaderOptions { LookForHeader = true, LeaveStreamOpen = false, Password = authentication };
    //         var archive = ArchiveFactory.Open(input, options);
    //
    //         root.Children = new List<LibraryPathItem>();
    //
    //         var pathToItem = new Dictionary<string, LibraryPathItem>();
    //         var nameToItem = new Dictionary<string, LibraryPathItem> { { "", root } };
    //         foreach (var entry in archive.Entries)
    //         {
    //             var path = entry.Key;
    //             var item = new LibraryPathItem
    //             {
    //                 IsNewItem = true,
    //                 FullName = Path.GetFileName(path.Last() == '/' ? path.Substring(0, path.Length - 1) : path),
    //             };
    //             item.PathType = entry.IsDirectory
    //                 ? LibraryPathType.Directory
    //                 : ILibraryHandler.ExtensionToLibraryPathType(item.Extension);
    //             if (ILibraryHandler.SpecialPathTypes.Contains(item.PathType))
    //             {
    //                 specialItems.Add(item);
    //             }
    //             pathToItem.Add(path, item);
    //             if (!entry.IsDirectory) continue;
    //             item.Children = new List<LibraryPathItem>();
    //             nameToItem.Add(Path.GetDirectoryName(path + (archive.Type == ArchiveType.Rar ? "/" : "")), item);
    //         }
    //
    //         foreach (var (path, child) in pathToItem)
    //         {
    //             var parent = nameToItem[Path.GetDirectoryName(path.TrimEnd('\\').TrimEnd('/'))];
    //             child.ParentId = parent.Id;
    //             child.Parent = parent;
    //             parent.Children.Add(child);
    //         }
    //
    //         if (root.Children.Count == 1 && root.Children.First().PathType == LibraryPathType.Directory)
    //         {
    //             var child = root.Children.First();
    //             root.Children = new List<LibraryPathItem>();
    //             foreach (var grandChild in child.Children)
    //             {
    //                 grandChild.FullName = Path.Combine(child.FullName, grandChild.FullName);
    //                 grandChild.ParentId = root.Id;
    //                 grandChild.Parent = root;
    //                 root.Children.Add(grandChild);
    //             }
    //         }
    //
    //         return true;
    //     }
    // }
}