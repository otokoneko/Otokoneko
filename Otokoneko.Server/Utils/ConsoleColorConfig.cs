using System;

namespace Otokoneko.Server.Utils
{
    public interface IConsoleColorConfig
    {
        public void SetForeground(ConsoleColor foreground);
    }

    public class DefaultConsoleColorConfig : IConsoleColorConfig
    {
        public void SetForeground(ConsoleColor foreground)
        {
            Console.ForegroundColor = foreground;
        }
    }

    public class AnsiConsoleColorConfig : IConsoleColorConfig
    {
        public void SetForeground(ConsoleColor foreground)
        {
            Console.Write(AnsiEscapeColorUtils.Encode(foreground));
        }
    }
}
