using System;
using System.Text;
using LevelDB;
using MessagePack;

namespace Otokoneko.Server.PluginManage
{
    public class PluginParameterProvider
    {
        private const string Path = "./pluginParameter";
        private DB DB { get; }

        public PluginParameterProvider()
        {
            DB = new DB(new Options() {CreateIfMissing = true}, Path);
        }

        public void Put(Type pluginType, string propertyName, object value)
        {
            DB.Put(Encoding.UTF8.GetBytes($"{pluginType.Name}.{propertyName}"), MessagePackSerializer.Serialize(value));
        }

        public object Get(Type pluginType, string propertyName, Type propertyType)
        {
            var bytes = DB.Get(Encoding.UTF8.GetBytes($"{pluginType.Name}.{propertyName}"));
            return bytes == null ? null : MessagePackSerializer.Deserialize(propertyType, bytes);
        }

        public void Delete(Type pluginType, string propertyName)
        {
            DB.Delete(Encoding.UTF8.GetBytes($"{pluginType.Name}.{propertyName}"));
        }
    }
}