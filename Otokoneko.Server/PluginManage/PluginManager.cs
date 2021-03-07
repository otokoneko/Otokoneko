using System.Collections.Generic;
using System.Linq;
using Otokoneko.DataType;
using Otokoneko.Plugins.Interface;

namespace Otokoneko.Server.PluginManage
{
    public class PluginManager
    {
        public PluginLoader PluginLoader { get; set; }
        public List<IMangaDownloader> MangaDownloaders { get; private set; }
        public List<IMetadataScraper> MetadataScrapers { get; private set; }

        public PluginManager(PluginLoader pluginLoader)
        {
            PluginLoader = pluginLoader;
            Reload();
        }

        public void Reload()
        {
            PluginLoader.Load();
            MangaDownloaders = PluginLoader.GetPlugins<IMangaDownloader>();
            MetadataScrapers = PluginLoader.GetPlugins<IMetadataScraper>();
        }

        public List<PluginDetail> GetPluginDetails()
        {
            return PluginLoader.PluginDetails.Values.ToList();
        }

        public PluginDetail GetPluginDetail(string pluginType)
        {
            return PluginLoader.PluginDetails.TryGetValue(pluginType, out var detail) ? detail : null;
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
