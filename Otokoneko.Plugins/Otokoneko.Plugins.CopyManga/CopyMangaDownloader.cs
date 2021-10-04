using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bert.RateLimiters;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Otokoneko.Plugins.Interface;

namespace Otokoneko.Plugins.CopyManga
{
    public class CopyMangaDownloader : IMangaDownloader
    {
        public string Name => nameof(CopyManga);
        public string Author => "Otokoneko";
        public Version Version => new Version(1, 1, 2);

        private string ChapterApiBase { get; } = "https://www.copymanga.com/comic/{0}/chapter/{1}";

        private static HttpClient Client { get; } = new HttpClient();

        private FixedTokenBucket Limiters { get; set; } = new FixedTokenBucket(1, 1, 300);

        #region RequiredParameters

        [RequiredParameter(typeof(string), @"^https?://(www\.)?copymanga\.com/comic/[^/]+$", alias: "漫画链接正则表达式")]
        public string MangaRe { get; set; }

        [RequiredParameter(typeof(string), @"^https?://(www\.)?copymanga\.com/comic/[^/]+/chapter/[^/]+$", alias: "章节链接正则表达式")]
        public string ChapterRe { get; set; }

        [RequiredParameter(typeof(string), @"^https?://?([^/]+\.)(cdn77.org)|(mangafunc.fun:12001)/.+$", alias: "图片链接正则表达式")]
        public string ImageRe { get; set; }

        [RequiredParameter(typeof(string), "xxxmanga.woo.key", alias: "漫画内容的解密密钥")]
        public string MangaKey { get; set; }

        [RequiredParameter(typeof(string), "xxxmanga.woo.key", alias: "章节内容的解密密钥")]
        public string ChapterKey { get; set; }

        [RequiredParameter(typeof(string), "/html/body/div[3]", alias: "章节内容的XPath")]
        public string ImageDataXPath { get; set; }

        [RequiredParameter(typeof(string), "author", alias: "标签类别“作者”的默认名称")]
        public string TagTypeAuthorName { get; set; }

        [RequiredParameter(typeof(string), "content", alias: "标签类别“内容”的默认名称")]
        public string TagTypeContentName { get; set; }

        [RequiredParameter(typeof(string), "status", alias: "标签类别“连载状态”的默认名称")]
        public string TagTypeStatusName { get; set; }

        #endregion

        #region JsonObject
        public class Response
        {
            public int code { get; set; }
            public string message { get; set; }
            public string results { get; set; }
        }
        public class Image
        {
            public string uuid { get; set; }
            public string url { get; set; }
        }
        public class Type
        {
            public int id { get; set; }
            public string name { get; set; }
        }

        public class Build
        {
            public string path_word { get; set; }
            public List<Type> type { get; set; }
        }

        public class Chapter
        {
            public int type { get; set; }
            public string name { get; set; }
            public string id { get; set; }
        }

        public class LastChapter
        {
            public int index { get; set; }
            public string uuid { get; set; }
            public int count { get; set; }
            public int ordered { get; set; }
            public int size { get; set; }
            public string name { get; set; }
            public string comic_id { get; set; }
            public string comic_path_word { get; set; }
            public object group_id { get; set; }
            public string group_path_word { get; set; }
            public int type { get; set; }
            public int img_type { get; set; }
            public string datetime_created { get; set; }
            public string prev { get; set; }
            public object next { get; set; }
        }

        public class Default
        {
            public string path_word { get; set; }
            public int count { get; set; }
            public string name { get; set; }
            public List<Chapter> chapters { get; set; }
            public LastChapter last_chapter { get; set; }
        }

        public class Groups
        {
            public Default @default { get; set; }
        }

        public class Manga
        {
            public Build build { get; set; }
            public Groups groups { get; set; }
        }

        #endregion

        public bool IsLegalUrl(string url, DownloadTaskType downloadTaskType)
        {
            var result = Regex.IsMatch(url, MangaRe);

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

        private async ValueTask<Manga> GetComicDetail(string mangaUrl)
        {
            var detailUrl = mangaUrl.Replace("comic", "comicdetail");
            if (detailUrl.Last() != '/')
            {
                detailUrl += '/';
            }
            detailUrl += "chapters";
            var respText = await Client.GetStringAsync(detailUrl);
            var resp = JsonConvert.DeserializeObject<Response>(respText);
            try
            {
                return Decrypt<Manga>(resp.results, MangaKey);
            }
            catch(Exception e)
            {
                throw new Exception("漫画内容解密失败", e);
            }
        }

        public async ValueTask<MangaDetail> GetManga(string url)
        {
            var html = await Client.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            var titleNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/main/div[1]/div/div[2]/ul/li[1]/h6");
            var aliasesNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/main/div[1]/div/div[2]/ul/li[2]/p");
            var authorsNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/main/div[1]/div/div[2]/ul/li[3]/span[2]");
            var contentNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/main/div[1]/div/div[2]/ul/li[7]/span[2]");
            var statusNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/main/div[1]/div/div[2]/ul/li[6]/span[2]");
            var coverNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/main/div[1]/div/div[1]/div/img");
            var descriptionNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/main/div[2]/div[2]/p");
            var subscribeNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/main/div[1]/div/div[2]/ul/li[8]/a[2]");

            var mangaDetail = await GetComicDetail(url);

            var tags = authorsNode.ChildNodes.Where(it => it.Name == "a").Select(authorsNodeChildNode => new TagDetail()
            {
                Type = TagTypeAuthorName,
                Name = authorsNodeChildNode.InnerText.Trim()
            }).ToList();
            tags.AddRange(contentNode.ChildNodes.Where(it => it.Name == "a").Select(authorsNodeChildNode => new TagDetail()
            {
                Type = TagTypeContentName, 
                Name = authorsNodeChildNode.InnerText.Trim()[1..]
            }));
            tags.Add(new TagDetail()
            {
                Type = TagTypeStatusName,
                Name = statusNode.InnerText.Trim()
            });

            var path_word = mangaDetail.build.path_word;
            var types = mangaDetail.build.type;

            return new MangaDetail
            {
                Id = subscribeNode.GetAttributeValue("onclick", "").Replace("collect('", "").Replace("')", ""),
                Name = titleNode.GetAttributeValue("title", ""),
                Aliases = aliasesNode.InnerText.Trim().Split(',').ToList(),
                Cover = coverNode.GetAttributeValue("data-src", null),
                Description = descriptionNode.InnerText,
                Url = url,
                Tags = tags,
                Chapters = mangaDetail.groups.@default.chapters.Select(it => new ChapterDetail
                {
                    Id = it.id,
                    Name = it.name,
                    Url = string.Format(ChapterApiBase, path_word, it.id),
                    Type = types.Single(t => t.id == it.type).name,
                }).ToList()
            };
        }

        public async ValueTask<List<string>> GetChapter(string url)
        {
            var html = await Client.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            var node = htmlDoc.DocumentNode.SelectSingleNode(ImageDataXPath);
            var contentkey = node?.GetAttributeValue("contentkey", null);
            if(string.IsNullOrEmpty(contentkey))
            {
                throw new Exception($"无法通过 XPath: {ImageDataXPath} 获取章节内容");
            }
            try
            {
                var detail = Decrypt<List<Image>>(contentkey, ChapterKey);
                return detail.Select(it => it.url).ToList();
            }
            catch (Exception e)
            {
                throw new Exception("章节内容解密失败", e);
            }
        }

        public async ValueTask<HttpContent> GetImage(string url)
        {
            while (Limiters.ShouldThrottle(1, out var delayTime)) await Task.Delay(delayTime);
            var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            return response.Content;
        }

        public T Decrypt<T>(string plain, string key)
        {
            var iv = Encoding.UTF8.GetBytes(plain.Substring(0, 0x10));
            plain = plain[0x10..];
            var toDecryptArray = Enumerable.Range(0, plain.Length / 2).Select(x => Convert.ToByte(plain.Substring(x * 2, 2), 16))
                .ToArray();
            var rijndaelManaged = new RijndaelManaged { IV = iv, Key = Encoding.UTF8.GetBytes(key), Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 };
            var decryptor = rijndaelManaged.CreateDecryptor();
            var result = decryptor.TransformFinalBlock(toDecryptArray, 0, toDecryptArray.Length);
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(result));
        }
    }
}
