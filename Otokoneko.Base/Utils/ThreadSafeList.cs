using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Otokoneko.Utils
{
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
                    return ((IList<T>)_data)[index];
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
                    ((IList<T>)_data)[index] = value;
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
                    return ((ICollection<T>)_data).Count;
                }
                finally
                {
                    _lock.ExitUpgradeableReadLock();
                }
            }
        }

        public bool IsReadOnly => ((ICollection<T>)_data).IsReadOnly;

        public void Add(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                ((ICollection<T>)_data).Add(item);
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
                ((ICollection<T>)_data).Clear();
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
                return ((ICollection<T>)_data).Contains(item);
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
                ((ICollection<T>)_data).CopyTo(array, arrayIndex);
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
                return ((IList<T>)_data).IndexOf(item);
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
                ((IList<T>)_data).Insert(index, item);
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
                return ((ICollection<T>)_data).Remove(item);
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
                ((IList<T>)_data).RemoveAt(index);
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