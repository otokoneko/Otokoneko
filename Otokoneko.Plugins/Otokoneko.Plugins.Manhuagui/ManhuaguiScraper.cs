using Bert.RateLimiters;
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
        private const string QueryBase = BaseUrl + "/s/{0}.html";

        private readonly Regex OtherInfoRe = new Regex(@"(\[[^\]]*\])|(【[^】]*】)|(（[^）]*）)|(\([^\)]*\))");

        private FixedTokenBucket ScrapeLimiters { get; set; }

        #region

        private int _scrapeIntervalMS;
        [RequiredParameter(typeof(int), 10000, alias: "限速设置，每两次获取元数据间需要间隔的时间（ms），设置为0则不做限速")]
        public int ScrapeIntervalMS
        {
            get => _scrapeIntervalMS;
            set
            {
                if (_scrapeIntervalMS == value) return;
                _scrapeIntervalMS = Math.Max(0, value);
                ScrapeLimiters = _scrapeIntervalMS == 0 ? null : new FixedTokenBucket(1, 1, _scrapeIntervalMS);
            }
        }

        #endregion

        private async ValueTask<HtmlDocument> Search(string title)
        {
            var queryUrl = string.Format(QueryBase, title);

            while (ScrapeLimiters != null && ScrapeLimiters.ShouldThrottle(1, out var delayTime)) await Task.Delay(delayTime);

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
                title = OtherInfoRe.Replace(title, "").Trim();
                htmlDoc = await Search(title);
            }

            if (htmlDoc == null)
            {
                title = Chinese.ChineseConverter.ToSimplified(title);
                htmlDoc = await Search(title);
            }

            if (htmlDoc == null) return;

            var searchResults = htmlDoc.DocumentNode.SelectNodes("//div[@class='book-detail']/dl/dt/a");

            var normalizedLevenshtein = new NormalizedLevenshtein();
            var results = searchResults.OrderBy(it => normalizedLevenshtein.Distance(title, it.GetAttributeValue("title", "")));
            var searchResult = results.FirstOrDefault();

            var mangaDetailUrl = searchResult?.GetAttributeValue("href", "");

            if (string.IsNullOrEmpty(mangaDetailUrl)) return;

            var mangaDetailUri = BaseUrl + mangaDetailUrl;

            while (ScrapeLimiters != null && ScrapeLimiters.ShouldThrottle(1, out var delayTime)) await Task.Delay(delayTime);

            var page = await Client.GetStringAsync(mangaDetailUri);
            var detail = ExtractMetadata(page);

            context.Name = detail.Name;
            context.Description = detail.Description;
            context.Aliases = detail.Aliases;
            context.Tags = detail.Tags;
        }
    }
}
