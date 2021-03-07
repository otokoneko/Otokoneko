using System;

namespace Otokoneko.Plugins.Interface
{
    public interface IPlugin
    {
        public string Name { get; }
        public string Author { get; }
        public Version Version { get; }
    }
}
