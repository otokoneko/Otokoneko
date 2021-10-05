using System.Collections.Generic;
using Otokoneko.DataType;
using Otokoneko.Plugins.Interface;

namespace Otokoneko.Server.PluginManage
{
    public class PluginManager
    {
        public PluginLoader PluginLoader { get; set; }
        public List<IMangaDownloader> MangaDownloaders => PluginLoader.GetPlugins<IMangaDownloader>();
        public List<IMetadataScraper> MetadataScrapers => PluginLoader.GetPlugins<IMetadataScraper>();

        public PluginManager(PluginLoader pluginLoader)
        {
            PluginLoader = pluginLoader;
            Reload();
        }

        public void Reload()
        {
            PluginLoader.Load();
        }

        public List<PluginDetail> GetPluginDetails()
        {
            return PluginLoader.GetPluginDetails();
        }

        public PluginDetail GetPluginDetail(string pluginType)
        {
            return PluginLoader.GetPluginDetail(pluginType);
        }

        public bool SetPluginParameters(PluginDetail detail)
        {
            return PluginLoader.SetParameters(detail);
        }

        public PluginDetail ResetPluginParameters(string pluginType)
        {
            PluginLoader.ResetParameters(pluginType);
            return GetPluginDetail(pluginType);
        }
    }
}
