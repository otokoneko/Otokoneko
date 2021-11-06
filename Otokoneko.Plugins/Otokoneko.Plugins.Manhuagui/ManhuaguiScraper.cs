using F23.StringSimilarity;
using HtmlAgilityPack;
using Otokoneko.Plugins.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Otokoneko.Plugins.Manhuagui
{
    public partial class ManhuaguiDownloader : IMetadataScraper
    {
        private const string QueryBase = "https://www.mhgui.com/s/{0}.html";

        private Regex OtherInfoRe = new Regex(@"(\[[^\]]*\])|(【[^】]*】)");

        public async ValueTask ScrapeMetadata(MangaDetail context)
        {
            var title = context.Name;

            var queryUrl = string.Format(QueryBase, title);
            var resp = await Client.GetStringAsync(queryUrl);

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(resp);

            if(htmlDoc.GetElementbyId("PanelNoResult") != null)
            {
                title = OtherInfoRe.Replace(title, "").Trim();
                if (string.IsNullOrEmpty(title)) return;
                queryUrl = string.Format(QueryBase, title);
                resp = await Client.GetStringAsync(queryUrl);
                htmlDoc.LoadHtml(resp);
                if (htmlDoc.GetElementbyId("PanelNoResult") != null) return;
            }

            var searchResults = htmlDoc.DocumentNode.SelectNodes("//div[@class='book-detail']/dl/dt/a");

            var normalizedLevenshtein = new NormalizedLevenshtein();
            var results = searchResults.OrderBy(it => normalizedLevenshtein.Distance(title, it.GetAttributeValue("title", "")));
            var searchResult = results.FirstOrDefault();

            var mangaDetailUrl = searchResult?.GetAttributeValue("href", "");

            if (string.IsNullOrEmpty(mangaDetailUrl)) return;

            var mangaDetailUri = new Uri(new Uri(QueryBase), mangaDetailUrl);

            var page = await Client.GetStringAsync(mangaDetailUri);
            var detail = ExtractMetadata(page);

            context.Name = detail.Name;
            context.Description = detail.Description;
            context.Aliases = detail.Aliases;
            context.Tags = detail.Tags;
        }
    }
}
