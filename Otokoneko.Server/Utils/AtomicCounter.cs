using System.Threading;

namespace Otokoneko.Server.Utils
{
    public class AtomicCounter
    {
        private int _current;
        public int Target { get; }

        public AtomicCounter(int current, int target)
        {
            _current = current;
            Target = target;
        }

        public bool IsCompleted()
        {
            return _current == Target;
        }

        public static AtomicCounter operator ++(AtomicCounter counter)
        {
            Interlocked.Increment(ref counter._current);
            return counter;
        }

        public static explicit operator double(AtomicCounter counter)
        {
            return (double)counter._current / counter.Target;
        }
    }

}
