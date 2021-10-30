#if CLIENT

using System;
using System.IO;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;

namespace Otokoneko.Utils
{
    public class Cache<TValue>
    {
        private readonly ObjectCache _memoryCache;
        private readonly FileCache _fileCache;
        private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1);

        public long Size => _fileCache.CurrentCacheSize;

        public Cache(string storePath, int maxMemorySize, int maxFileSize)
        {
            Directory.CreateDirectory(storePath);
            foreach (var file in Directory.GetFiles(storePath))
            {
                if (long.TryParse(Path.GetFileName(file), out _))
                {
                    File.Delete(file);
                }
            }

            var config = new System.Collections.Specialized.NameValueCollection
            {
                { "cacheMemoryLimitMegabytes", $"{maxMemorySize / 1024 / 1024}" }
            };

            _memoryCache = new MemoryCache(nameof(_memoryCache), config);

            _fileCache = new FileCache(storePath, true)
            {
                MaxCacheSize = maxFileSize,
            };
        }

        public async ValueTask<TValue> Get(string key)
        {
            await _mutex.WaitAsync();
            try
            {
                var value = _memoryCache.Get(key) ?? _fileCache.Get(key);
                return (TValue)value;
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async ValueTask<bool> Contain(string key)
        {
            await _mutex.WaitAsync();
            try
            {
                return _memoryCache.Contains(key) || _fileCache.Contains(key);
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async ValueTask Add(string key, TValue value)
        {
            await _mutex.WaitAsync();
            try
            {
                _memoryCache.Add(key, value, DateTimeOffset.MaxValue);
                _fileCache.Add(key, value, DateTimeOffset.MaxValue);
            }
            finally
            {
                _mutex.Release();
            }
        }

        public void Clear()
        {
            _fileCache.Clear();
        }
    }

}

#endif