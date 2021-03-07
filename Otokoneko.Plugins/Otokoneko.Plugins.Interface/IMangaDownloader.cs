using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Otokoneko.Plugins.Interface
{
    public class MangaDetail
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Cover { get; set; }
        public string Description { get; set; }
        public string Url { get; set; }
        public List<string> Aliases { get; set; }
        public List<ChapterDetail> Chapters { get; set; }
        public List<TagDetail> Tags { get; set; }
    }

    public class ChapterDetail
    {
        public string Type { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
    }

    public class TagDetail
    {
        public string Type { get; set; }
        public string Name { get; set; }
    }

    public enum DownloadTaskType
    {
        Manga,
        Chapter,
        Image
    }

    public interface IMangaDownloader: IPlugin
    {
        public bool IsLegalUrl(string url, DownloadTaskType downloadTaskType);
        public ValueTask<MangaDetail> GetManga(string url);
        public ValueTask<List<string>> GetChapter(string url);
        public ValueTask<HttpContent> GetImage(string url);
    }
}
