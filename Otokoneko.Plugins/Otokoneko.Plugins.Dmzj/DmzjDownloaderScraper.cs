using Chinese;
using Newtonsoft.Json;
using Otokoneko.Plugins.Interface;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Otokoneko.Plugins.Dmzj
{
    public partial class DmzjDownloader : IMetadataScraper
    {
        private const string QueryBase = "https://sacg.dmzj.com/comicsum/search.php?s={0}";

        #region JsonObject

        public class SearchResultItem
        {
            public int id { get; set; }
            public string comic_name { get; set; }
            public string comic_author { get; set; }
            public string comic_cover { get; set; }
            public string cover { get; set; }
            public string last_update_chapter_name { get; set; }
            public string comic_url_raw { get; set; }
            public string comic_url { get; set; }
            public string status { get; set; }
            public string chapter_url_raw { get; set; }
            public string chapter_url { get; set; }
        }

        #endregion

        public async ValueTask ScrapeMetadata(MangaDetail context)
        {
            var queryUrl = string.Format(QueryBase, ChineseConverter.ToSimplified(context.Name));
            var resp = await Client.GetStringAsync(queryUrl);

            var result = JsonConvert.DeserializeObject<List<SearchResultItem>>(resp[20..^1]).FirstOrDefault();
            if (result == null) return;

            var mangaId = result.id.ToString();
            var comicUrl = $"https:{result.comic_url}";

            var newDetail = await GetMangaByV4Api(mangaId) ?? await GetMangaByV1Api(mangaId);
            if (newDetail == null) return;

            var mangaPage = await Client.GetStringAsync(comicUrl);

            context.Name = newDetail.Name;
            context.Description = newDetail.Description;
            context.Tags = newDetail.Tags;
            context.Aliases = ExtractAliases(mangaPage);
        }
    }
}
