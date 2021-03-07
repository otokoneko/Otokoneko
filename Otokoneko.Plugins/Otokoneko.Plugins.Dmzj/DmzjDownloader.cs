using Otokoneko.Plugins.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;


namespace Otokoneko.Plugins.Dmzj
{
    public class DmzjDownloader : IMangaDownloader
    {
        public string Name => nameof(Dmzj);
        public string Author => "Otokoneko";
        public Version Version => new Version(1, 0, 0);
        private static string MangaApiBase { get; } = "https://v3api.dmzj1.com/comic/comic_{0}.json";
        private static string ChapterApiBase { get; } = "https://m.dmzj1.com/view/{0}/{1}.html";
        private static Regex MangaIdRe { get; } = new Regex("(?:(?:obj_id)|(?:g_current_id)) = \"([0-9]+)\"");
        private static Regex ImageListRe { get; } = new Regex("\"page_url\":(.+?]),");
        private static HttpClient Client { get; } = new HttpClient();

        #region RequiredParameters

        [RequiredParameter(typeof(string), @"^https?://manhua.dmzj1?.com/[^/]+/?$", alias: "漫画链接正则表达式")]
        public string MangaRe { get; set; }

        [RequiredParameter(typeof(string), @"^https?://m.dmzj1?.com/view/[0-9]+/[0-9]+\.html/?$", alias: "章节链接正则表达式")]
        public string ChapterRe { get; set; }

        [RequiredParameter(typeof(string), @"^https?://images.dmzj1?.com/.+$", alias: "图片链接正则表达式")]
        public string ImageRe { get; set; }

        [RequiredParameter(typeof(string), "author", nameof(TagTypeAuthorName), alias:"标签类别“作者”的默认名称")]
        public string TagTypeAuthorName { get; set; }

        [RequiredParameter(typeof(string), "content", nameof(TagTypeContentName), alias:"标签类别“内容”的默认名称")]
        public string TagTypeContentName { get; set; }

        [RequiredParameter(typeof(string), "status", nameof(TagTypeStatusName), alias:"标签类别“连载状态”的默认名称")]
        public string TagTypeStatusName { get; set; }

        #endregion

        #region JsonObject

        public class Type
        {
            public int tag_id { get; set; }
            public string tag_name { get; set; }
        }

        public class Author_
        {
            public int tag_id { get; set; }
            public string tag_name { get; set; }
        }

        public class Status
        {
            public int tag_id { get; set; }
            public string tag_name { get; set; }
        }

        public class Datum
        {
            public int chapter_id { get; set; }
            public string chapter_title { get; set; }
            public int updatetime { get; set; }
            public int filesize { get; set; }
            public int chapter_order { get; set; }
        }

        public class Chapter
        {
            public string title { get; set; }
            public List<Datum> data { get; set; }
        }

        public class LatestComment
        {
            public int comment_id { get; set; }
            public int uid { get; set; }
            public string content { get; set; }
            public int createtime { get; set; }
            public string nickname { get; set; }
            public string avatar { get; set; }
        }

        public class Comment
        {
            public int comment_count { get; set; }
            public List<LatestComment> latest_comment { get; set; }
        }

        public class DhUrlLink
        {
            public string title { get; set; }
            public List<object> list { get; set; }
        }

        public class MangaDetail_
        {
            public int id { get; set; }
            public int islong { get; set; }
            public int direction { get; set; }
            public string title { get; set; }
            public int is_dmzj { get; set; }
            public string cover { get; set; }
            public string description { get; set; }
            public int last_updatetime { get; set; }
            public string last_update_chapter_name { get; set; }
            public int copyright { get; set; }
            public string first_letter { get; set; }
            public string comic_py { get; set; }
            public int hidden { get; set; }
            public int hot_num { get; set; }
            public int hit_num { get; set; }
            public object uid { get; set; }
            public int is_lock { get; set; }
            public int last_update_chapter_id { get; set; }
            public List<Type> types { get; set; }
            public List<Author_> authors { get; set; }
            public List<Status> status { get; set; }
            public int subscribe_num { get; set; }
            public List<Chapter> chapters { get; set; }
            public Comment comment { get; set; }
            public int is_need_login { get; set; }
            public List<object> url_links { get; set; }
            public string isHideChapter { get; set; }
            public List<DhUrlLink> dh_url_links { get; set; }
            public string is_dot { get; set; }
        }

        #endregion

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

        public async ValueTask<MangaDetail> GetManga(string url)
        {
            var page = await Client.GetStringAsync(url);
            var idMatch = MangaIdRe.Match(page);
            if (!idMatch.Success) return null;
            var mangaId = idMatch.Groups[1].Value;

            List<string> aliases = null;
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(page);
            var aliasesNode = htmlDoc.DocumentNode.SelectSingleNode(
                "/html/body/div[4]/div[3]/div/div[1]/div[2]/div/div[4]/table/tbody/tr[1]/td");
            if (!string.IsNullOrEmpty(aliasesNode?.InnerText?.Trim()))
            {
                aliases = aliasesNode.InnerText.Trim().Split(',').ToList();
            }
            page = await Client.GetStringAsync(string.Format(MangaApiBase, mangaId));
            var mangaDetail = JsonConvert.DeserializeObject<MangaDetail_>(page);
            var result = new MangaDetail()
            {
                Cover = mangaDetail.cover,
                Id = mangaId,
                Name = mangaDetail.title,
                Description = mangaDetail.description,
                Tags = new List<TagDetail>(),
                Aliases = aliases,
                Url = url,
                Chapters = mangaDetail.chapters.SelectMany(chapters => chapters.data.Select(chapter =>
                    new ChapterDetail()
                    {
                        Id = chapter.chapter_id.ToString(),
                        Name = chapter.chapter_title,
                        Type = chapters.title,
                        Url = string.Format(ChapterApiBase, mangaId, chapter.chapter_id)
                    })).Reverse().ToList()
            };
            result.Tags.AddRange(mangaDetail.authors.Select(author => new TagDetail()
            { Name = author.tag_name, Type = TagTypeAuthorName }));
            result.Tags.AddRange(mangaDetail.types.Select(type => new TagDetail()
            { Name = type.tag_name, Type = TagTypeContentName }));
            result.Tags.AddRange(mangaDetail.status.Select(status => new TagDetail()
            { Name = status.tag_name, Type = TagTypeStatusName }));
            return result;
        }

        public async ValueTask<List<string>> GetChapter(string url)
        {
            var page = await Client.GetStringAsync(url);
            var imageListMatch = ImageListRe.Match(page);
            if (!imageListMatch.Success) return null;
            var imageList = JsonConvert.DeserializeObject<List<string>>(imageListMatch.Groups[1].Value);
            return imageList;
        }

        public async ValueTask<HttpContent> GetImage(string url)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers =
                {
                    Referrer = url.Contains("webpic") ? new Uri("https://m.dmzj.com") : new Uri("http://manhua.dmzj1.com")
                }
            };
            var response = await Client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
            return response.Content;
        }
    }
}
