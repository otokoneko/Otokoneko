using F23.StringSimilarity;
using HtmlAgilityPack;
using Otokoneko.Plugins.Interface;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Otokoneko.Plugins.Manhuagui
{
    public partial class ManhuaguiDownloader : IMetadataScraper
    {
        private const string QueryBase = BaseUrl + "/s/{0}.html";

        private readonly Regex OtherInfoRe = new Regex(@"(\[[^\]]*\])|(【[^】]*】)|(（[^）]*）)|(\([^\)]*\))");

        private async ValueTask<HtmlDocument> Search(string title)
        {
            var queryUrl = string.Format(QueryBase, title);

            var resp = await Client.GetStringAsync(queryUrl);

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(resp);

            return htmlDoc.GetElementbyId("PanelNoResult") != null ? null : htmlDoc;
        }

        public async ValueTask ScrapeMetadata(MangaDetail context)
        {
            var title = context.Name.Replace("_", "");

            var htmlDoc = await Search(title);

            if (htmlDoc == null)
            {
                var newTitle = OtherInfoRe.Replace(title, "").Trim();
                if (title != newTitle)
                {
                    title = newTitle;
                    htmlDoc = await Search(title);
                }
            }

            if (htmlDoc == null)
            {
                var newTitle = Chinese.ChineseConverter.ToSimplified(title);
                if (title != newTitle)
                {
                    title = newTitle;
                    htmlDoc = await Search(title);
                }
            }

            if (htmlDoc == null) return;

            var searchResults = htmlDoc.DocumentNode.SelectNodes("//div[@class='book-detail']/dl/dt/a");

            var normalizedLevenshtein = new NormalizedLevenshtein();
            var results = searchResults.OrderBy(it => normalizedLevenshtein.Distance(title, it.GetAttributeValue("title", "")));
            var searchResult = results.FirstOrDefault();

            var mangaDetailUrl = searchResult?.GetAttributeValue("href", "");

            if (string.IsNullOrEmpty(mangaDetailUrl)) return;

            var mangaDetailUri = BaseUrl + mangaDetailUrl;

            var page = await Client.GetStringAsync(mangaDetailUri);
            var detail = ExtractMetadata(page);

            context.Name = detail.Name;
            context.Description = detail.Description;
            context.Aliases = detail.Aliases;
            context.Tags = detail.Tags;
        }
    }
}
