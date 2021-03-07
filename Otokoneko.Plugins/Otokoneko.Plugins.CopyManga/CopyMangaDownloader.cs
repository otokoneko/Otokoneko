using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Otokoneko.Plugins.Interface;

namespace Otokoneko.Plugins.CopyManga
{
    public class CopyMangaDownloader : IMangaDownloader
    {
        public string Name => nameof(CopyManga);
        public string Author => "Otokoneko";
        public Version Version => new Version(1, 0, 0);

        private static byte[] Key { get; } = Encoding.UTF8.GetBytes("hotmanga.aes.key");
        private string ChapterApiBase { get; } = "https://www.copymanga.com/comic/{0}/chapter/{1}";

        private static HttpClient Client { get; } = new HttpClient();

        #region RequiredParameters

        [RequiredParameter(typeof(string), @"^https?://(www\.)?copymanga\.com/comic/[^/]+$", alias: "漫画链接正则表达式")]
        public string MangaRe { get; set; }

        [RequiredParameter(typeof(string), @"^https?://(www\.)?copymanga\.com/comic/[^/]+/chapter/[^/]+$", alias: "章节链接正则表达式")]
        public string ChapterRe { get; set; }

        [RequiredParameter(typeof(string), @"^https?://?([^.]+\.)mangafunc.fun/comic/.+$", alias: "图片链接正则表达式")]
        public string ImageRe { get; set; }

        [RequiredParameter(typeof(string), "author", alias: "标签类别“作者”的默认名称")]
        public string TagTypeAuthorName { get; set; }

        [RequiredParameter(typeof(string), "content", alias: "标签类别“内容”的默认名称")]
        public string TagTypeContentName { get; set; }

        [RequiredParameter(typeof(string), "status", alias: "标签类别“连载状态”的默认名称")]
        public string TagTypeStatusName { get; set; }

        #endregion

        #region JsonObject

        public class ChapterDetail_
        {
            public string uuid { get; set; }
            public string url { get; set; }
        }

        public class 全部
        {
            public int index { get; set; }
            public string uuid { get; set; }
            public int count { get; set; }
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
            public string next { get; set; }
        }

        public class 話
        {
            public int index { get; set; }
            public string uuid { get; set; }
            public int count { get; set; }
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
            public string next { get; set; }
        }

        public class 番外篇
        {
            public int index { get; set; }
            public string uuid { get; set; }
            public int count { get; set; }
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
            public string next { get; set; }
        }

        public class Groups
        {
            public List<全部> 全部 { get; set; }
            public List<話> 話 { get; set; }
            public List<object> 卷 { get; set; }
            public List<番外篇> 番外篇 { get; set; }
        }

        public class LastChapter
        {
            public int index { get; set; }
            public string uuid { get; set; }
            public int count { get; set; }
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
            public Groups groups { get; set; }
            public LastChapter last_chapter { get; set; }
        }

        public class MangaDetail_
        {
            public Default @default { get; set; }
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
            var disposableNode = htmlDoc.DocumentNode.SelectSingleNode("/html/body/main/div[3]");
            var disposable = disposableNode.GetAttributeValue("disposable", null);
            var mangaDetail = Decrypt<MangaDetail_>(disposable);

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

            return new MangaDetail
            {
                Id = subscribeNode.GetAttributeValue("onclick", "").Replace("collect('", "").Replace("')", ""),
                Name = titleNode.GetAttributeValue("title", ""),
                Aliases = aliasesNode.InnerText.Trim().Split(',').ToList(),
                Cover = coverNode.GetAttributeValue("data-src", null),
                Description = descriptionNode.InnerText,
                Url = url,
                Tags = tags,
                Chapters = mangaDetail.@default.groups.全部.Select(it => new ChapterDetail
                {
                    Id = it.uuid,
                    Name = it.name,
                    Url = string.Format(ChapterApiBase, it.comic_path_word, it.uuid)
                }).ToList()
            };
        }

        public async ValueTask<List<string>> GetChapter(string url)
        {
            var html = await Client.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            var node = htmlDoc.DocumentNode.SelectSingleNode("/html/body/div[2]");
            var disposable = node.GetAttributeValue("disposable", null);
            var detail = Decrypt<List<ChapterDetail_>>(disposable);
            return detail.Select(it => it.url).ToList();
        }

        public async ValueTask<HttpContent> GetImage(string url)
        {
            var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            return response.Content;
        }

        public T Decrypt<T>(string plain)
        {
            var iv = Encoding.UTF8.GetBytes(plain.Substring(0, 0x10));
            plain = plain[0x10..];
            var toDecryptArray = Enumerable.Range(0, plain.Length / 2).Select(x => Convert.ToByte(plain.Substring(x * 2, 2), 16))
                .ToArray();
            var rijndaelManaged = new RijndaelManaged { IV = iv, Key = Key, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 };
            var decryptor = rijndaelManaged.CreateDecryptor();
            var result = decryptor.TransformFinalBlock(toDecryptArray, 0, toDecryptArray.Length);
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(result));
        }
    }
}
