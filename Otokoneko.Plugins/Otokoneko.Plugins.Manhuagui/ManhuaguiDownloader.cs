using Bert.RateLimiters;
using HtmlAgilityPack;
using LZStringCSharp;
using Newtonsoft.Json;
using Otokoneko.Plugins.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Otokoneko.Plugins.Manhuagui
{
    public partial class ManhuaguiDownloader : IMangaDownloader
    {
        public string Name => nameof(Manhuagui);

        public string Author => "Otokoneko";

        public Version Version => new Version(1, 0, 1);

        private static HttpClient Client { get; } = new HttpClient();

        private FixedTokenBucket DownloadImageLimiters { get; set; }

        #region RequiredParameters

        [RequiredParameter(typeof(string), @"^https?:\/\/(tw.)?(www.)?((mhgui)|(manhuagui))\.com\/comic\/[0-9]+\/?$", alias: "漫画链接正则表达式")]
        public string MangaRe { get; set; }

        [RequiredParameter(typeof(string), @"^https?:\/\/(tw.)?(www.)?((mhgui)|(manhuagui))\.com\/comic\/[0-9]+\/[0-9]+\.html$", alias: "章节链接正则表达式")]
        public string ChapterRe { get; set; }

        [RequiredParameter(typeof(string), @"^https?:\/\/[^.]+\.hamreus\.com\/.+$", alias: "图片链接正则表达式")]
        public string ImageRe { get; set; }

        [RequiredParameter(typeof(string), "author", alias: "标签类别“作者”的默认名称")]
        public string TagTypeAuthorName { get; set; }

        [RequiredParameter(typeof(string), "content", alias: "标签类别“内容”的默认名称")]
        public string TagTypeContentName { get; set; }

        [RequiredParameter(typeof(string), "status", alias: "标签类别“连载状态”的默认名称")]
        public string TagTypeStatusName { get; set; }

        [RequiredParameter(typeof(string), "https://i.hamreus.com", alias: "图片服务器")]
        public string ImageServer { get; set; }

        private int _downloadImageIntervalMS;
        [RequiredParameter(typeof(int), 10000, alias: "限速设置，每两次下载图片间需要间隔的时间（ms），设置为0则不做限速")]
        public int DownloadImageIntervalMS
        {
            get => _downloadImageIntervalMS;
            set
            {
                if (_downloadImageIntervalMS == value) return;
                _downloadImageIntervalMS = Math.Max(0, value);
                DownloadImageLimiters = _downloadImageIntervalMS == 0 ? null : new FixedTokenBucket(1, 1, _downloadImageIntervalMS);
            }
        }

        #endregion

        #region JsonObject

        public class ImageData
        {
            public int bid { get; set; }
            public string bname { get; set; }
            public string bpic { get; set; }
            public int cid { get; set; }
            public string cname { get; set; }
            public List<string> files { get; set; }
            public bool finished { get; set; }
            public int len { get; set; }
            public string path { get; set; }
            public int status { get; set; }
            public string block_cc { get; set; }
            public int nextId { get; set; }
            public int prevId { get; set; }
            public Dictionary<string, string> sl { get; set; }
        }

        #endregion

        private Regex WindowRe { get; set; } = new Regex("window\\[\".*?\"\\]\\(.*\\)\\s*\\{[\\s\\S]+\\}\\s*\\(.*\\)\\s");
        private Regex ImgDataRe { get; set; } = new Regex(@";\}\((?<js>.+)\,[0-9]+\,[0-9]+\,\'(?<map>[^']+)[^,]+\,?[0-9]+\,\{\}\)");
        private Regex ImgDataJsonRe { get; set; } = new Regex(@"\{.*\}");

        private string AlphaBeta { get; set; } = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        private const string BaseUrl = "http://www.manhuagui.com";

        public ManhuaguiDownloader()
        {
            Client.DefaultRequestHeaders.UserAgent.ParseAdd(@"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/97.0.4688.4 Safari/537.36");
            Client.DefaultRequestHeaders.Referrer = new Uri(BaseUrl);
        }

        public bool IsLegalUrl(string url, DownloadTaskType downloadTaskType)
        {
            switch (downloadTaskType)
            {
                case DownloadTaskType.Manga:
                    return Regex.IsMatch(url, MangaRe);
                case DownloadTaskType.Chapter:
                    return Regex.IsMatch(url, ChapterRe);
                case DownloadTaskType.Image:
                    return Regex.IsMatch(url, ImageRe);
            }

            return false;
        }

        private string Encode(int idx)
        {
            if (idx < AlphaBeta.Length) return AlphaBeta[idx].ToString();
            return Encode(idx / AlphaBeta.Length) + AlphaBeta[idx % AlphaBeta.Length].ToString();
        }

        public async ValueTask<List<string>> GetChapter(string url)
        {
            var page = await Client.GetStringAsync(url);
            var imgDataString = WindowRe.Match(page).Groups[0].Value;
            var imgDataMatch = ImgDataRe.Match(imgDataString);
            var js = imgDataMatch.Groups["js"].Value;
            var map = imgDataMatch.Groups["map"].Value;

            map = LZString.DecompressFromBase64(map);
            int idx = 0;
            foreach(var to in map.Split('|'))
            {
                var from = Encode(idx++);
                if (string.IsNullOrEmpty(to)) continue; 
                js = Regex.Replace(js, $"\\b{from}\\b", to);
            }
            var json = ImgDataJsonRe.Match(js).Value;

            var imageData = JsonConvert.DeserializeObject<ImageData>(json);

            var query = string.Join('&', imageData.sl.Select(it => $"{it.Key}={it.Value}"));

            return imageData.files.Select(file => $"{ImageServer}{imageData.path}{file}?{query}").ToList();
        }

        public async ValueTask<HttpContent> GetImage(string url)
        {
            while (DownloadImageLimiters != null && DownloadImageLimiters.ShouldThrottle(1, out var delayTime)) await Task.Delay(delayTime);

            var requestMessage = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers =
                {
                    Referrer = new Uri("https://www.mhgui.com/"),
                }
            };
            var response = await Client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
            return response.Content;
        }

        private MangaDetail ExtractMetadata(string page)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(page);

            var coverNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/div[5]/div[1]/div[1]/div[1]/p/img");
            var titleNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/div[5]/div[1]/div[1]/div[2]/div[2]/h1");
            var descNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/div[5]/div[1]/div[1]/div[2]/div[3]/div[2]");
            var tagNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/div[5]/div[1]/div[1]/div[2]/ul/li[2]/span[1]");
            var authorNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/div[5]/div[1]/div[1]/div[2]/ul/li[2]/span[2]");
            var statusNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/div[5]/div[1]/div[1]/div[2]/ul/li[4]/span/span[1]");

            var result = new MangaDetail()
            {
                Cover = coverNode.GetAttributeValue("src", ""),
                Name = titleNode.InnerText,
                Description = string.Join('\n', descNode.ChildNodes.Select(it => HtmlEntity.DeEntitize(it.InnerText))),
                Tags = new List<TagDetail>(),
                Chapters = new List<ChapterDetail>(),
                Aliases = new List<string>()
            };

            var aliase = titleNode.NextSibling.InnerText;
            if (!string.IsNullOrEmpty(aliase))
            {
                result.Aliases.Add(aliase);
            }

            result.Tags.AddRange(tagNode.Descendants("a").Select(it =>
            new TagDetail
            {
                Type = TagTypeContentName,
                Name = it.GetAttributeValue("title", "")
            }));

            result.Tags.AddRange(authorNode.Descendants("a").Select(it =>
            new TagDetail
            {
                Type = TagTypeAuthorName,
                Name = it.GetAttributeValue("title", "")
            }));

            result.Tags.Add(
            new TagDetail
            {
                Type = TagTypeStatusName,
                Name = statusNode.InnerText.Trim()
            });

            var aliasNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/div[5]/div[1]/div[1]/div[2]/ul/li[3]/span");
            result.Aliases.AddRange(aliasNode.Descendants("a").Select(it => it.InnerText));

            return result;
        }

        public async ValueTask<MangaDetail> GetManga(string url)
        {
            var page = await Client.GetStringAsync(url);

            url = url.Trim('/');
            var mangaId = url.Split('/').Last();

            var result = ExtractMetadata(page);
            result.Id = mangaId;

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(page);

            HtmlNode firstNode;
            var viewStateNode = htmlDoc.GetElementbyId("__VIEWSTATE");

            if (viewStateNode != null)
            {
                var viewStateNodeValue = viewStateNode.GetAttributeValue("value", "");
                var partDoc = new HtmlDocument();
                partDoc.LoadHtml(LZString.DecompressFromBase64(viewStateNodeValue));
                firstNode = partDoc.DocumentNode.FirstChild;
            }
            else
            {
                firstNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/div[5]/div[1]/div[4]/div[1]");
            }

            while (firstNode.Name != "h4")
            {
                firstNode = firstNode.NextSibling;
            }

            for (var node = firstNode; node != null; )
            {
                var chapterClass = node.InnerText;
                while (!node.GetAttributeValue("class", "").Contains("chapter-list"))
                {
                    node = node.NextSibling;
                }

                var chaptersWithSameType = new List<ChapterDetail>();
                var chapterCollections = node.Descendants("ul");
                foreach (var chapterCollection in chapterCollections.Reverse())
                {
                    foreach (var chapter in chapterCollection.Descendants("li"))
                    {
                        var chapterNode = chapter.FirstChild;
                        var chapterId = chapterNode.GetAttributeValue("href", "").Replace(".html", "").Split('/').Last();
                        chaptersWithSameType.Add(new ChapterDetail
                        {
                            Id = chapterId,
                            Name = chapterNode.GetAttributeValue("title", ""),
                            Url = $"{url}/{chapterId}.html",
                            Type = chapterClass
                        });
                    }
                }
                chaptersWithSameType.Reverse();
                result.Chapters.AddRange(chaptersWithSameType);

                while (node != null && node.Name != "h4")
                {
                    node = node.NextSibling;
                }
            }

            return result;
        }
    }
}
