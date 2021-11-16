#if CLIENT

using System;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;

namespace Otokoneko.Utils
{
    public class Cache<TValue>
    {
        public TimeSpan TTL { get; set; }
        private MemoryCache _memoryCache;
        private FileCache _fileCache;
        public string CachePath { get; set; }
        private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1);
        private readonly NameValueCollection _memoryCacheConfig;

        public long Size => _fileCache.GetCacheSize();

        private long _maxFileCacheSize;
        public long MaxFileCacheSize
        {
            get => _fileCache.MaxCacheSize;
            set => _fileCache.MaxCacheSize = _maxFileCacheSize = value;
        }

        public Cache(string cachePath, long maxMemorySize, long maxFileSize, TimeSpan ttl)
        {
            CachePath = cachePath;
            Directory.CreateDirectory(CachePath);
            foreach (var file in Directory.GetFiles(CachePath))
            {
                if (long.TryParse(Path.GetFileName(file), out _))
                {
                    File.Delete(file);
                }
            }

            _memoryCacheConfig = new NameValueCollection
            {
                { "cacheMemoryLimitMegabytes", $"{maxMemorySize / 1024 / 1024}" }
            };
            _memoryCache = new MemoryCache(nameof(_memoryCache), _memoryCacheConfig);

            _maxFileCacheSize = maxFileSize;
            _fileCache = new FileCache(CachePath, true)
            {
                MaxCacheSize = _maxFileCacheSize,
            };

            TTL = ttl;
        }

        public async ValueTask<TValue> Get(string key)
        {
            await _mutex.WaitAsync();
            try
            {
                var value = _memoryCache.Get(key);
                if (value == null)
                {
                    value = _fileCache.Get(key);
                    if (value != null)
                    {
                        _memoryCache.Add(key, value, DateTimeOffset.Now.Add(TTL));
                    }
                }
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
                _memoryCache.Add(key, value, DateTimeOffset.Now.Add(TTL));
                _fileCache.Add(key, value, DateTimeOffset.Now.Add(TTL));
            }
            finally
            {
                _mutex.Release();
            }
        }

        public void Clear()
        {
            _mutex.Wait();
            try
            {
                _fileCache.Clear();
                _fileCache = new FileCache(CachePath, true)
                {
                    MaxCacheSize = _maxFileCacheSize,
                };
                _memoryCache.Dispose();
                _memoryCache = new MemoryCache(nameof(_memoryCache), _memoryCacheConfig);
            }
            finally
            {
                _mutex.Release();
            }
        }
    }

}

#endif