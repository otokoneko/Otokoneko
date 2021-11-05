using System;
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
        public void Delete(FileTreeRoot library, string path);

        public bool CreateFileTree(FileTreeNode root, IdGenerator idGenerator);
    }

    public interface IFileTreeNodeHandler
    {
        public static Dictionary<string, IFileTreeNodeHandler> Handlers { get; } = new Dictionary<string, IFileTreeNodeHandler>();
        public HashSet<string> SupportExtensions { get; }

        public Stream OpenWrite(Stream input, string path);
        public Stream OpenRead(Stream input, string path);
        public void Delete(string path);

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

        public void Delete(FileTreeRoot library, string path)
        {
            File.Delete(Path.Combine(library.Path, path));
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
            _archive.Dispose();
            _data.Dispose();
        }
    }

    public class ArchiveFileHandler : IFileTreeNodeHandler
    {
        public HashSet<string> SupportExtensions => new()
        {
            ".zip",
            ".rar",
            ".7z",
            ".tar",
            ".gz"
        };

        public bool CreateFileTree(Stream input, FileTreeNode root, IdGenerator idGenerator)
        {
            var options = new ReaderOptions { LookForHeader = true, LeaveStreamOpen = false };
            using var archive = ArchiveFactory.Open(input, options);

            root.Children = new List<FileTreeNode>();

            var pathToItem = new Queue<Tuple<string, FileTreeNode>>();
            var nameToItem = new Dictionary<string, FileTreeNode> { { "", root } };
            foreach (var entry in archive.Entries)
            {
                var path = entry.Key.TrimEnd('\\').TrimEnd('/');
                var item = new FileTreeNode
                {
                    ObjectId = idGenerator.CreateId(),
                    IsNewItem = true,
                    FullName = Path.GetFileName(path),
                    IsDirectory = entry.IsDirectory,
                    Library = root.Library
                };
                pathToItem.Enqueue(new Tuple<string, FileTreeNode>(path, item));
                if (!entry.IsDirectory) continue;
                item.Children = new List<FileTreeNode>();
                nameToItem.Add(path, item);
            }

            while (pathToItem.Count != 0)
            {
                var (path, child) = pathToItem.Dequeue();
                var parentPath = Path.GetDirectoryName(path);
                if(!nameToItem.TryGetValue(parentPath, out var parent))
                {
                    parent = new FileTreeNode
                    {
                        ObjectId = idGenerator.CreateId(),
                        IsNewItem = true,
                        FullName = Path.GetFileName(parentPath),
                        IsDirectory = true,
                        Library = root.Library,
                        Children = new List<FileTreeNode>()
                    };
                    nameToItem[parentPath] = parent;
                    pathToItem.Enqueue(new Tuple<string, FileTreeNode>(parentPath, parent));
                }
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
            var options = new ReaderOptions { LookForHeader = true, LeaveStreamOpen = true };
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

        public Stream OpenWrite(Stream input, string path)
        {
            throw new NotImplementedException();
        }

        public void Delete(string path)
        {
            throw new NotImplementedException();
        }
    }

    public class EpubFileHandler : IFileTreeNodeHandler
    {
        public HashSet<string> SupportExtensions => new()
        {
            ".epub",
        };

        public bool CreateFileTree(Stream input, FileTreeNode root, IdGenerator idGenerator)
        {
            var options = new ReaderOptions { LookForHeader = true, LeaveStreamOpen = false };
            using var archive = ArchiveFactory.Open(input, options);

            root.Children = new List<FileTreeNode>();

            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory) continue;
                var path = entry.Key.TrimEnd('\\').TrimEnd('/');
                var item = new FileTreeNode
                {
                    ObjectId = idGenerator.CreateId(),
                    IsNewItem = true,
                    FullName = path,
                    IsDirectory = entry.IsDirectory,
                    Library = root.Library,
                    ParentId = root.ObjectId,
                    Parent = root,
                };

                root.Children.Add(item);
            }

            return true;
        }

        public Stream OpenRead(Stream input, string path)
        {
            var options = new ReaderOptions { LookForHeader = true, LeaveStreamOpen = true };
            var archive = ArchiveFactory.Open(input, options);
            path = archive.Type != ArchiveType.Rar ? path.Replace('\\', '/') : path.Replace('/', '\\');
            var entry = archive.Entries.Single(e => e.Key == path);
            var stream = entry.OpenEntryStream();
            return new ArchiveStream(stream, archive);
        }

        public static void Register()
        {
            var instance = new EpubFileHandler();
            foreach (var extension in instance.SupportExtensions)
            {
                IFileTreeNodeHandler.Handlers.Add(extension, instance);
            }
        }

        public Stream OpenWrite(Stream input, string path)
        {
            throw new NotImplementedException();
        }

        public void Delete(string path)
        {
            throw new NotImplementedException();
        }
    }
}