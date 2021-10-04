using LevelDB;
using MessagePack;
using Otokoneko.DataType;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using IdGen;
using log4net;
using Otokoneko.Server.Converter;
using Otokoneko.Server.LibraryManage;
using Otokoneko.Server.Utils;

namespace Otokoneko.Server
{
    public class LibraryManager
    {
        private static string LibraryPath { get; } = @"./data/library";
        private static string ThumbnailPath { get; } = @"./data/thumbnail";
        private static IdGenerator IdGenerator { get; } = new IdGenerator(0);

        private readonly Options _dbOptions;
        private readonly DB _libraryDb;
        private readonly MessagePackSerializerOptions _lz4Options =
            MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);

        private ConcurrentDictionary<long, FileTreeRoot> _libraries;

        static LibraryManager()
        {
            FileSystemHandler.Register();
            ArchiveFileHandler.Register();
        }

        public LibraryManager(ILog logger)
        {
            logger.Info("加载路径信息...");
            _dbOptions = new Options() {CreateIfMissing = true};
            _libraryDb = new DB(_dbOptions, Path.Combine(LibraryPath, "metadata"));
            RecoverLibrary();
            CreateThumbnailLibrary();
        }

        private void RecoverLibrary()
        {
            _libraries = new ConcurrentDictionary<long, FileTreeRoot>();
            foreach (var (_, bytes) in _libraryDb)
            {
                var library = MessagePackSerializer.Deserialize<FileTreeRoot>(bytes, _lz4Options);
                library.Repository = new LevelDbFileTreeNodeRepository(
                    library,
                    new DB(_dbOptions, Path.Combine(LibraryPath, library.ObjectId.ToString())),
                    (bytes1 => MessagePackSerializer.Deserialize<FileTreeNode>(bytes1, _lz4Options)),
                    (node => MessagePackSerializer.Serialize(node, _lz4Options)));
                _libraries.TryAdd(library.ObjectId, library);
            }
        }

        private void CreateThumbnailLibrary()
        {
            if (_libraries.ContainsKey(0)) return;
            var library = new FileTreeRoot()
            {
                ObjectId = 0,
                Path = ThumbnailPath,
                Scheme = "file"
            };
            AddLibrary(library, false);
        }

        public FileTreeRoot AddLibrary(FileTreeRoot library, bool generateId = true)
        {
            if (generateId) library.ObjectId = IdGenerator.CreateId();
            if (!IFileTreeRootHandler.Handlers.TryGetValue(library.Scheme, out var handler) ||
                !handler.IsLegal(library)) return null;

            _libraryDb.Put(BitConverter.GetBytes(library.ObjectId),
                MessagePackSerializer.Serialize(library, _lz4Options));
            _libraries.TryAdd(library.ObjectId, library);
            library.Repository = new LevelDbFileTreeNodeRepository(
                library,
                new DB(_dbOptions, Path.Combine(LibraryPath, library.ObjectId.ToString())),
                (bytes1 => MessagePackSerializer.Deserialize<FileTreeNode>(bytes1, _lz4Options)),
                (node => MessagePackSerializer.Serialize(node, _lz4Options)));
            return library;

        }

        public bool UpdateLibrary(FileTreeRoot library)
        {
            if (!_libraries.TryGetValue(library.ObjectId, out var oldLibrary) || !Monitor.TryEnter(oldLibrary, 0)) return false;
            try
            {
                oldLibrary.Name = library.Name;
                oldLibrary.Path = library.Path;
                oldLibrary.Host = library.Host;
                oldLibrary.Port = library.Port;
                oldLibrary.Username = library.Username;
                oldLibrary.Password = library.Password;
                oldLibrary.Scheme = library.Scheme;
                oldLibrary.ScraperName = library.ScraperName;
                _libraryDb.Put(BitConverter.GetBytes(library.ObjectId), MessagePackSerializer.Serialize(library, _lz4Options));
                return true;
            }
            finally
            {
                Monitor.Exit(oldLibrary);
            }
        }

        public List<FileTreeRoot> GetLibraries()
        {
            return _libraries.Values.Where(it => it.ObjectId != 0).ToList();
        }

        public FileTreeRoot GetLibrary(long libraryId)
        {
            return _libraries.TryGetValue(libraryId, out var library) ? library : null;
        }
        
        public void Delete(FileTreeRoot library)
        {
            _libraries.TryRemove(library.ObjectId, out _);
            _libraryDb.Delete(BitConverter.GetBytes(library.ObjectId));
            library.Repository.Close();
            DirectoryUtils.Delete(Path.Combine(LibraryPath, library.ObjectId.ToString()));
        }

        private bool RemoveIgnoreNodes(FileTreeNode root)
        {
            if (!root.IsDirectory || root.Children.Count == 0) return false;
            if (root.Children.Any(it => it.FullName == ".ignore"))
            {
                return true;
            }

            for (var i = 0; i < root.Children.Count; i++)
            {
                var node = root.Children[i];
                if (!RemoveIgnoreNodes(node)) continue;
                root.Children.RemoveAt(i);
                i--;
            }

            return false;
        }

        private void CreateSpecialNodes(FileTreeNode root)
        {
            if (root.IsNewItem && IFileTreeNodeHandler.Handlers.TryGetValue(root.Extension, out var handler))
            {
                using var stream = root.OpenRead();
                handler.CreateFileTree(stream, root, IdGenerator);
            }

            if (!root.IsDirectory) return;
            foreach (var child in root.Children)
            {
                CreateSpecialNodes(child);
            }
        }

        private void CreateFileTree(FileTreeNode root)
        {
            if (IFileTreeRootHandler.Handlers.TryGetValue(root.Library.Scheme, out var libraryHandler))
            {
                libraryHandler.CreateFileTree(root, IdGenerator);
            }

            CreateSpecialNodes(root);

            RemoveIgnoreNodes(root);
        }

        public List<FileTreeNode> CheckUpdates(long libraryId)
        {
            var library = GetLibrary(libraryId);
            if (library == null) throw new ArgumentException($"Can not find library by {nameof(libraryId)} {libraryId}");

            var updatedMangaPaths = new List<FileTreeNode>();

            if (!Monitor.TryEnter(library, 10000)) return updatedMangaPaths;

            try
            {
                var root = library.Repository.GetTree(0);
                CreateFileTree(root);

                foreach (var manga in root.Children.Where(it => it.Children != null && it.Children.Count > 0))
                {
                    if (manga.IsNewItem)
                    {
                        if (FileTreeNodeFormatter.ClassifyAndFormatDirectoryStruct(manga) == FileStructType.None) continue;
                        manga.StructType = FileStructType.Manga;
                        updatedMangaPaths.Add(manga);
                    }
                    else
                    {
                        var number = Enum.GetValues(typeof(FileStructType)).Cast<FileStructType>().Max();
                        var paths = new List<FileTreeNode>[(int)(number + 1)];
                        for (var i = 0; i < (int)(number + 1); i++)
                        {
                            paths[i] = new List<FileTreeNode>();
                        }

                        FileTreeNodeFormatter.ClassifyAndFormatDirectoryStruct(manga, paths);
                        if (paths[(int)FileStructType.Chapter].Any(it => it.IsNewItem))
                        {
                            updatedMangaPaths.Add(manga);
                        }
                    }
                }

                return updatedMangaPaths;
            }
            finally
            {
                Monitor.Exit(library);
            }
        }

        public FileTreeNode GeFileTreeNode(long objectId)
        {
            return (from library in _libraries.Values where library.Repository.Contains(objectId) select library.Repository.Get(objectId)).FirstOrDefault();
        }

        public bool StoreFileTree(FileTreeNode node)
        {
            return node.Library.Repository.StoreTree(node);
        }

        public bool Delete(FileTreeNode node)
        {
            foreach (var library in _libraries.Values)
            {
                if (!library.Repository.Contains(node.ObjectId)) continue;
                library.Repository.DeleteTree(node);
                return true;
            }

            return false;
        }

        public FileTreeNode GenerateThumbnail(FileTreeNode source)
        {
            var library = _libraries[0];
            var root = library.Repository.Get(0);
            var cover = new FileTreeNode()
            {
                ObjectId = IdGenerator.CreateId(),
                IsNewItem = true,
                StructType = FileStructType.Image,
                Parent = root,
                ParentId = root.ObjectId,
                Library = library
            };
            cover.FullName = $"{cover.ObjectId}";
            using var inStream = source.OpenRead();
            using var outStream = cover.OpenWrite();
            ImageUtils.ZoomImage(inStream, outStream, 600, 600);
            inStream.Close();
            outStream.Close();
            return cover;
        }
    }
}