using System;
using System.Collections.Generic;
using MessagePack;

namespace Otokoneko.DataType
{
    [MessagePackObject]
    public class PluginParameter
    {
        [Key(0)]
        public Type Type { get; set; }
        [Key(1)]
        public string Name { get; set; }
        [Key(2)]
        public string Alias { get; set; }
        [Key(3)]
        public object Value { get; set; }
        [Key(4)]
        public bool IsReadOnly { get; set; }
        [Key(5)]
        public string Comment { get; set; }
    }

    [MessagePackObject]
    public class PluginDetail
    {
        [Key(0)]
        public string Name { get; set; }
        [Key(1)]
        public Version Version { get; set; }
        [Key(2)]
        public string Author { get; set; }
        [Key(3)]
        public string Type { get; set; }
        [Key(4)]
        public List<string> SupportInterface { get; set; }
        [Key(5)]
        public List<PluginParameter> RequiredParameters { get; set; }
    }
}
