using Newtonsoft.Json;
using Otokoneko.Plugins.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Otokoneko.Plugins.CopyManga
{
    public partial class CopyMangaDownloader : IMetadataScraper
    {
        private const string QueryBase = "https://www.copymanga.com/api/kb/web/searchs/comics?offset=0&platform=2&limit=12&q={0}&q_type=";

        private const string MangaDetailBase = "https://www.copymanga.com/comic/{0}";

        #region JsonObject

        public class JsonAuthor
        {
            public string name { get; set; }
            public string alias { get; set; }
            public string path_word { get; set; }
        }

        public class List
        {
            public string name { get; set; }
            public string alias { get; set; }
            public string path_word { get; set; }
            public string cover { get; set; }
            public List<JsonAuthor> author { get; set; }
            public int popular { get; set; }
            public List<object> theme { get; set; }
            public List<object> parodies { get; set; }
            public List<object> females { get; set; }
            public List<object> males { get; set; }
        }

        public class Results
        {
            public List<List> list { get; set; }
            public int total { get; set; }
            public int limit { get; set; }
            public int offset { get; set; }
        }

        public class SearchResponse
        {
            public int code { get; set; }
            public string message { get; set; }
            public Results results { get; set; }
        }


        #endregion

        public async ValueTask ScrapeMetadata(MangaDetail context)
        {
            var url = string.Format(QueryBase, context.Name);
            var resp = await Client.GetStringAsync(url);

            var searchResp = JsonConvert.DeserializeObject<SearchResponse>(resp);
            var manga = searchResp.results.list.FirstOrDefault();

            if (manga == null) return;

            var mangaUrl = string.Format(MangaDetailBase, manga.path_word);
            var page = await Client.GetStringAsync(mangaUrl);

            var detail = ExtractMetadata(page);

            context.Name = detail.Name;
            context.Description = detail.Description;
            context.Aliases = detail.Aliases;
            context.Tags = detail.Tags;
        }
    }
}
