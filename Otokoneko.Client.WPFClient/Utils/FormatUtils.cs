using System;
using System.Linq;

namespace Otokoneko.Client.WPFClient.Utils
{
    public static class FormatUtils
    {
        private static readonly string[] SizeSuffixes = { "KB", "MB", "GB" };

        public static string FormatLocalDateTime(DateTime? localDateTime)
        {
            if (localDateTime == null) return string.Empty;
            var time = (DateTime)localDateTime;
            return
                $"{time.Year:D4}.{time.Month:D2}.{time.Day:D2} {time.Hour:D2}:{time.Minute:D2}";
        }

        public static string FormatSizeOfBytes(double size)
        {
            foreach (var suffix in SizeSuffixes)
            {
                size /= 1024;
                if (size < 1024)
                {
                    return $"{size:F} {suffix}";
                }
            }
            return $"{size:F} {SizeSuffixes.Last()}";
        }

    }
}
