using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Priority_Queue;

namespace Otokoneko.Utils
{
    public static class PasswordHashProvider
    {
        private const int HashByteSize = 64;

        public static string CreateHash(string username, string password, int iterations = 1 << 15)
        {
            using var sha512 = SHA512.Create();
            var salt = sha512.ComputeHash(Encoding.UTF8.GetBytes(username));
            var hash = Pbkdf2(password, salt, iterations, HashByteSize);
            var sb = new StringBuilder();
            foreach (var _ in hash)
            {
                sb.Append(_.ToString("X2"));
            }
            return sb.ToString();
        }

        public static string CreateHash(byte[] salt, string password, int iterations = 1 << 15)
        {
            using var sha512 = SHA512.Create();
            var hash = Pbkdf2(password, salt, iterations, HashByteSize);
            var sb = new StringBuilder();
            foreach (var _ in hash)
            {
                sb.Append(_.ToString("X2"));
            }
            return sb.ToString();
        }

        private static byte[] Pbkdf2(string password, byte[] salt, int iterations, int outputBytes)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt) { IterationCount = iterations };
            return pbkdf2.GetBytes(outputBytes);
        }
    }

    internal class FileCache
    {
        private readonly ConcurrentDictionary<long, string> _data;

        private readonly string _storePath;

        public long Size { get; private set; }

        public ICollection<long> Keys => _data.Keys;

        public int Count => _data.Count;

        public void Add(long key, byte[] value)
        {
            var path = Path.Combine(_storePath, key.ToString());
            if (!_data.TryAdd(key, path)) return;
            File.WriteAllBytes(path, value);
            Size += value.Length;
        }

        public async ValueTask<byte[]> Get(long key)
        {
            return await File.ReadAllBytesAsync(_data[key]);
        }

        public void Clear()
        {
            foreach (var path in _data.Values)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            Size = 0;
            _data.Clear();
        }

        public bool Contains(long key)
        {
            return _data.ContainsKey(key);
        }

        public void Remove(long key)
        {
            if (!_data.TryRemove(key, out var path) || !File.Exists(path)) return;
            var fileInfo = new FileInfo(path);
            Size -= fileInfo.Length;
            File.Delete(path);
        }

        public FileCache(string storePath)
        {
            _storePath = storePath;
            _data = new ConcurrentDictionary<long, string>();
            Size = 0;
            foreach (var file in Directory.GetFiles(storePath))
            {
                var fileInfo = new FileInfo(file);
                if (long.TryParse(fileInfo.Name, out var key))
                {
                    _data.TryAdd(key, file);
                    Size += fileInfo.Length;
                }
            }
        }
    }

    internal class InMemoryCache
    {
        private readonly ConcurrentDictionary<long, byte[]> _data;

        public long Size { get; private set; }

        public ICollection<long> Keys => _data.Keys;

        public int Count => _data.Count;

        public void Add(long key, byte[] value)
        {
            if(_data.TryAdd(key, value))
                Size += value.Length;
        }

        public async ValueTask<byte[]> Get(long key)
        {
            return _data[key];
        }

        public void Clear()
        {
            Size = 0;
            _data.Clear();
        }

        public bool Contains(long key)
        {
            return _data.ContainsKey(key);
        }

        public void Remove(long key)
        {
            if(_data.TryRemove(key, out var data))
                Size -= data.Length;
        }

        public InMemoryCache()
        {
            _data = new ConcurrentDictionary<long, byte[]>();
            Size = 0;
        }
    }

    public class LruCache
    {
        private readonly InMemoryCache _inMemoryCache;
        private readonly FileCache _fileCache;
        private readonly List<long> _fileCacheKeys;
        private readonly List<long> _inMemoryCacheKeys;
        private readonly int _maxMemorySize;
        private readonly int _maxFileSize;
        private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1);

        public LruCache(string storePath, int maxMemorySize, int maxFileSize)
        {
            Directory.CreateDirectory(storePath);
            _maxFileSize = maxFileSize;
            _maxMemorySize = maxMemorySize;
            _inMemoryCache = new InMemoryCache();
            _fileCache = new FileCache(storePath);
            _fileCacheKeys = _fileCache.Keys.ToList();
            _inMemoryCacheKeys = _inMemoryCache.Keys.ToList();
        }

        public async ValueTask<byte[]> Get(long key)
        {
            await _mutex.WaitAsync();
            try
            {
                bool inFile = _fileCache.Contains(key), inMemory = _inMemoryCache.Contains(key);
                if (inFile)
                {
                    _fileCacheKeys.Remove(key);
                    _fileCacheKeys.Add(key);
                }

                if (inMemory)
                {
                    _inMemoryCacheKeys.Remove(key);
                    _inMemoryCacheKeys.Add(key);
                    return await _inMemoryCache.Get(key);
                }

                var value = await _fileCache.Get(key);
                _inMemoryCache.Add(key, value);
                _inMemoryCacheKeys.Add(key);
                if (_inMemoryCache.Size > _maxMemorySize)
                {
                    _inMemoryCache.Remove(_inMemoryCacheKeys.First());
                    _inMemoryCacheKeys.RemoveAt(0);
                }

                return value;
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async ValueTask<bool> Contain(long key)
        {
            await _mutex.WaitAsync();
            try
            {
                return _inMemoryCache.Contains(key) || _fileCache.Contains(key);
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async ValueTask Add(long key, byte[] value)
        {
            await _mutex.WaitAsync();
            try
            {
                _fileCacheKeys.Add(key);
                _fileCache.Add(key, value);
                _inMemoryCacheKeys.Add(key);
                _inMemoryCache.Add(key, value);
                if (_fileCache.Size > _maxFileSize)
                {
                    _fileCache.Remove(_fileCacheKeys.First());
                    _fileCacheKeys.RemoveAt(0);
                }
                if (_inMemoryCache.Size > _maxMemorySize)
                {
                    _inMemoryCache.Remove(_inMemoryCacheKeys.First());
                    _inMemoryCacheKeys.RemoveAt(0);
                }
            }
            finally
            {
                _mutex.Release();
            }
        }
    }

    public class AsyncQueue<TPriority, T> where TPriority : IComparable<TPriority>
    {
        private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1);
        private readonly SemaphoreSlim _full = new SemaphoreSlim(0);
        private readonly SimplePriorityQueue<T, TPriority> _priorityQueue = new SimplePriorityQueue<T, TPriority>();

        public async ValueTask Enqueue(T item, TPriority priority)
        {
            await _mutex.WaitAsync();
            try
            {
                _priorityQueue.Enqueue(item, priority);
                _full.Release();
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async ValueTask<T> Dequeue()
        {
            await _full.WaitAsync();
            await _mutex.WaitAsync();
            try
            {
                var result = _priorityQueue.Dequeue();
                return result;
            }
            finally
            {
                _mutex.Release();
            }
        }
    }

    public class ThreadSafeList<T> : IList<T>
    {
        private readonly List<T> _data;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public ThreadSafeList()
        {
            _data = new List<T>();
        }

        public ThreadSafeList(IEnumerable<T> data)
        {
            _data = new List<T>(data);
        }

        public T this[int index]
        {
            get
            {
                _lock.EnterUpgradeableReadLock();
                try
                {
                    return ((System.Collections.Generic.IList<T>)_data)[index];
                }
                finally
                {
                    _lock.ExitUpgradeableReadLock();
                }
            }
            set
            {
                _lock.EnterWriteLock();
                try
                {
                    ((System.Collections.Generic.IList<T>)_data)[index] = value;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }

        public int Count
        {
            get
            {
                _lock.EnterUpgradeableReadLock();
                try
                {
                    return ((System.Collections.Generic.ICollection<T>)_data).Count;
                }
                finally
                {
                    _lock.ExitUpgradeableReadLock();
                }
            }
        }

        public bool IsReadOnly => ((System.Collections.Generic.ICollection<T>)_data).IsReadOnly;

        public void Add(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                ((System.Collections.Generic.ICollection<T>)_data).Add(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                ((System.Collections.Generic.ICollection<T>)_data).Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool Contains(T item)
        {
            _lock.EnterUpgradeableReadLock();
            try
            {
                return ((System.Collections.Generic.ICollection<T>)_data).Contains(item);
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _lock.EnterWriteLock();
            try
            {
                ((System.Collections.Generic.ICollection<T>)_data).CopyTo(array, arrayIndex);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            _lock.EnterUpgradeableReadLock();
            try
            {
                var data = new List<T>(_data);
                foreach (var d in data)
                {
                    yield return d;
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        public int IndexOf(T item)
        {
            _lock.EnterUpgradeableReadLock();
            try
            {
                return ((System.Collections.Generic.IList<T>)_data).IndexOf(item);
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        public void Insert(int index, T item)
        {
            _lock.EnterWriteLock();
            try
            {
                ((System.Collections.Generic.IList<T>)_data).Insert(index, item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool Remove(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                return ((System.Collections.Generic.ICollection<T>)_data).Remove(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void RemoveAt(int index)
        {
            _lock.EnterWriteLock();
            try
            {
                ((System.Collections.Generic.IList<T>)_data).RemoveAt(index);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public List<T> GetAndClearAll()
        {
            _lock.EnterWriteLock();
            try
            {
                var result = _data.ToList();
                _data.Clear();
                return result;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}