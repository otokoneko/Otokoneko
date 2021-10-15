using System;
using System.Drawing;

namespace Otokoneko.Server.Utils
{
    public static class AnsiEscapeColorUtils
    {
        public static Color GetColor(ConsoleColor c)
        {
            int[] cColors = {
                    0x000000, //Black = 0
                    0x000080, //DarkBlue = 1
                    0x008000, //DarkGreen = 2
                    0x008080, //DarkCyan = 3
                    0x800000, //DarkRed = 4
                    0x800080, //DarkMagenta = 5
                    0x808000, //DarkYellow = 6
                    0xC0C0C0, //Gray = 7
                    0x808080, //DarkGray = 8
                    0x0000FF, //Blue = 9
                    0x00FF00, //Green = 10
                    0x00FFFF, //Cyan = 11
                    0xFF0000, //Red = 12
                    0xFF00FF, //Magenta = 13
                    0xFFFF00, //Yellow = 14
                    0xFFFFFF  //White = 15
                };
            return Color.FromArgb(cColors[(int)c]);
        }

        public static string Encode(Color foreground)
        {
            return $"\u001b[38;2;{foreground.R};{foreground.G};{foreground.B}m";
        }

        public static string Encode(ConsoleColor foreground)
        {
            var foregroundColor = GetColor(foreground);
            return $"\u001b[38;2;{foregroundColor.R};{foregroundColor.G};{foregroundColor.B}m";
        }

        public static string ResetColor()
        {
            return "\u001b[0m";
        }
    }
}
