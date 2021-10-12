using System;

namespace Otokoneko.Client.WPFClient.Utils
{
    public static class MathUtils
    {
        public static T Min<T>(T v1, T v2) where T : IComparable<T>
        {
            return (v1.CompareTo(v2) < 0 ? v1 : v2);
        }

        public static T Max<T>(T v1, T v2) where T : IComparable<T>
        {
            return (v1.CompareTo(v2) > 0 ? v1 : v2);
        }

        public static T LimitValue<T>(T origin, T min, T max) where T : IComparable<T>
        {
            return Max(Min(origin, max), min);
        }
    }
}
