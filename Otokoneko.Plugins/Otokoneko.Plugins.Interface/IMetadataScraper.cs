using System.Threading.Tasks;

namespace Otokoneko.Plugins.Interface
{
    public interface IMetadataScraper: IPlugin
    {
        public ValueTask ScrapeMetadata(MangaDetail context);
    }
}
