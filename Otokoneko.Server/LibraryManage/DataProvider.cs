using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LevelDB;
using Otokoneko.DataType;

namespace Otokoneko.Server.LibraryManage
{
    public interface IFileTreeNodeRepository
    {
        public FileTreeNode Get(long objectId);
        public bool Delete(FileTreeNode node);
        public bool Put(FileTreeNode node);
        public bool Contains(long objectId);
        public FileTreeNode GetTree();
        public List<long> GetAllNodeObjectId();
        public void Close();
    }

    public class LevelDbFileTreeNodeRepository : IFileTreeNodeRepository
    {
        private readonly DB _db;
        private readonly Func<byte[], FileTreeNode> _deserializer;
        private readonly Func<FileTreeNode, byte[]> _serializer;
        private readonly HashSet<long> _items;
        private readonly ConcurrentDictionary<long, FileTreeNode> _cache;
        private readonly FileTreeRoot _library;

        public FileTreeNode Get(long objectId)
        {
            if (_cache.TryGetValue(objectId, out var node)) return node;
            var bytes = _db.Get(BitConverter.GetBytes(objectId));
            if (bytes == null) return null;
            node = _deserializer(bytes);
            node.Library = _library;
            if(node.ObjectId != 0)
                node.Parent = Get(node.ParentId);
            _cache.TryAdd(node.ObjectId, node);
            return node;
        }

        public bool Delete(FileTreeNode node)
        {
            if (node.Children != null && node.Children.Any(child => !Delete(child)))
            {
                return false;
            }
            _db.Delete(BitConverter.GetBytes(node.ObjectId));
            _items.Remove(node.ObjectId);
            _cache.TryRemove(node.ObjectId, out _);
            return true;
        }

        public bool Put(FileTreeNode node)
        {
            if (node.Children != null && node.Children.Any(child => !Put(child)))
            {
                return false;
            }
            var bytes = _serializer(node);
            _db.Put(BitConverter.GetBytes(node.ObjectId), bytes);
            _items.Add(node.ObjectId);
            return true;
        }

        public bool Contains(long objectId)
        {
            return _items.Contains(objectId);
        }

        public FileTreeNode GetTree()
        {
            var deleted = new HashSet<FileTreeNode>();
            var mapper = new Dictionary<long, FileTreeNode>();

            foreach (var (_, bytes) in _db)
            {
                var node = _deserializer(bytes);
                node.Library = _library;
                mapper.Add(node.ObjectId, node);
            }

            foreach (var child in mapper.Values)
            {
                if (child.ObjectId == 0) continue;
                if (mapper.TryGetValue(child.ParentId, out var parent))
                {
                    child.Parent = parent;
                    child.Parent.Children ??= new List<FileTreeNode>();
                    child.Parent.Children.Add(child);
                }
                else
                {
                    deleted.Add(child);
                }
            }

            foreach (var node in deleted)
            {
                Delete(node);
            }

            return mapper[0];
        }

        public List<long> GetAllNodeObjectId()
        {
            return _items.ToList();
        }

        public void Close()
        {
            _db.Close();
        }

        public LevelDbFileTreeNodeRepository(FileTreeRoot library, DB db, Func<byte[], FileTreeNode> deserializer, Func<FileTreeNode, byte[]> serializer)
        {
            _library = library;
            _db = db;
            _deserializer = deserializer;
            _serializer = serializer;
            _cache = new ConcurrentDictionary<long, FileTreeNode>();
            _items = new HashSet<long>();
            foreach (var (id, _) in _db)
            {
                _items.Add(BitConverter.ToInt64(id));
            }
            if (!Contains(0))
            {
                Put(FileTreeNode.Root);
            }
        }
    }
}
