using System;
using System.Threading;
using System.Threading.Tasks;
using Priority_Queue;

namespace Otokoneko.Utils
{
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
}
