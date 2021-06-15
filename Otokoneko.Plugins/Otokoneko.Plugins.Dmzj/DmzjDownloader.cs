using Otokoneko.Plugins.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Dmzj;


namespace Otokoneko.Plugins.Dmzj
{
    public class RetryHandler : DelegatingHandler
    {
        private const int MaxRetries = 3;

        public RetryHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        { }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            for (var i = 0; i < MaxRetries; i++)
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                await Task.Delay(new Random().Next(1000, 10000), cancellationToken);
            }

            return null;
        }
    }


    public class DmzjDownloader : IMangaDownloader
    {
        public string Name => nameof(Dmzj);
        public string Author => "Otokoneko";
        public Version Version => new Version(1, 0, 0);
        private static string MangaApiBase { get; } = "https://nnv4api.muwai.com/comic/detail/{0}?uid=1";
        private static string ChapterApiBase { get; } = "https://m.dmzj1.com/view/{0}/{1}.html";
        private static Regex MangaIdRe { get; } = new Regex("(?:(?:obj_id)|(?:g_current_id)) = \"([0-9]+)\"");
        private static Regex ImageListRe { get; } = new Regex("\"page_url\":(.+?]),");
        private static HttpClient Client { get; } = new HttpClient(new RetryHandler(new HttpClientHandler()));

        private byte[] PrivateKey { get; } = Convert.FromBase64String(
            "MIICXgIBAAKBgQCvJzUdZU5yHyHrOqEViTY95gejrLAxsdLhjKYKW1QqX+vlcJ7iNrLZoWTaEHDONeyM+1qpT821JrvUeHRCpixhBKjoTnVWnofV5NiDz46iLuU25C2UcZGN3STNYbW8+e3f66HrCS5GV6rLHxuRCWrjXPkXAAU3y2+CIhY0jJU7JwIDAQABAoGBAIs/6YtoSjiSpb3Ey+I6RyRo5/PpS98GV/i3gB5Fw6E4x2uO4NJJ2GELXgm7/mMDHgBrqQVoi8uUcsoVxaBjSm25737TGCueoR/oqsY7Qy540gylp4XAe9PPbDSmhDPSJYpersVjKzDAR/b9jy3WLKjAR6j7rSrv0ooHhj3oge1RAkEA4s1ZTb+u4KPfUACL9p/4GuHtMC4s1bmjQVxPPAHTp2mdCzk3p4lRKrz7YFJOt8245dD/6c0M8o4rcHuh6AgCKQJBAMWzrZwptbihKeR7DWlxCU8BO1kH+z6yw+PgaRrTSpII2un+heJXeEGdk0Oqr7Aos0hia4zqTXY1Rie24GDHHM8CQQC7yVjy5g4u06BXxkwdBLDR2VShOupGf/Ercfns7npHuEueel6Zajn5UAY2549j4oMATf9Gn0/kGVDgTo1s6AyZAkApc6PqA0DLxlbPRhGo0v99pid4YlkGa1rxM4M2Eakn911XBHuz2l0nfM98t5QAnngArEoakKHPMBpWh1yCTh03AkEAmcOddu2RrPGQ00q6IKx+9ysPx71+ecBgHoqymHL9vHmrr3ghu4shUdDxQfz/xA2Z8m/on78hBZbnD1CNPmPOxQ==");

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

        internal static byte[] RsaDecrypt(byte[] encryptedBytes, RSACryptoServiceProvider rsa)
        {
            using var decrypted = new MemoryStream();
            var length = encryptedBytes.Length;
            var blockSize = rsa.KeySize / 8;
            for (var offset = 0; offset < length; offset += blockSize)
            {
                var bufferLength = Math.Min(blockSize, length - offset);
                var buffer = encryptedBytes.Skip(offset).Take(bufferLength).ToArray();
                var decryptedData = rsa.Decrypt(buffer, false);
                decrypted.Write(decryptedData, 0, decryptedData.Length);
            }
            decrypted.Position = 0;
            return decrypted.ToArray();
        }

        private byte[] Decrypt(string data)
        {
            var bytes = Convert.FromBase64String(data);
            using var rsa = new RSACryptoServiceProvider();
            rsa.ImportRSAPrivateKey(PrivateKey, out _);
            return RsaDecrypt(bytes, rsa);
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

            var apiUrl = string.Format(MangaApiBase, mangaId);
            var apiResponse = await Client.GetStringAsync(apiUrl);
            var mangaDetailResponse = MangaDetailResponse.Parser.ParseFrom(Decrypt(apiResponse));

            var mangaDetail = mangaDetailResponse.Manga;
            var result = new MangaDetail()
            {
                Cover = mangaDetail.Cover,
                Id = mangaId,
                Name = mangaDetail.Title,
                Description = mangaDetail.Descrition,
                Tags = new List<TagDetail>(),
                Aliases = aliases,
                Url = url,
                Chapters = mangaDetail.Volume.SelectMany(volume => volume.Chapter.OrderBy(chapter => chapter.Order)
                    .Select(chapter =>
                        new ChapterDetail()
                        {
                            Id = chapter.Id.ToString(),
                            Name = chapter.Name,
                            Type = volume.Name,
                            Url = string.Format(ChapterApiBase, mangaId, chapter.Id)
                        })).ToList()
            };
            result.Tags.AddRange(mangaDetail.Author.Select(author => new TagDetail()
            { Name = author.Name, Type = TagTypeAuthorName }));
            result.Tags.AddRange(mangaDetail.Tag.Select(type => new TagDetail()
            { Name = type.Name, Type = TagTypeContentName }));
            result.Tags.AddRange(mangaDetail.Status.Select(status => new TagDetail()
            { Name = status.Name, Type = TagTypeStatusName }));
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
