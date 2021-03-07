using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Otokoneko.Plugins.Interface;

namespace Otokoneko.Plugins.NHentai
{
    public class NHentai: IMetadataScraper
    {
        public string Name => nameof(NHentai);
        public string Author => "Otokoneko";
        public Version Version => new Version(1, 0, 0);

        private static HttpClient Client { get; } = new HttpClient();

        private const string QueryBase = "https://nhentai.net/search/?q={0}";
        private const string Host = "https://nhentai.net";

        [RequiredParameter(typeof(string), "parody", alias: "标签类别“Parodies”的默认名称")]
        public string ParodiesTagTypeName { get; set; }

        [RequiredParameter(typeof(string), "character", alias: "标签类别“Characters”的默认名称")]
        public string CharactersTagTypeName { get; set; }

        [RequiredParameter(typeof(string), "content", alias: "标签类别“Tags”的默认名称")]
        public string TagsTagTypeName { get; set; }

        [RequiredParameter(typeof(string), "author", alias: "标签类别“Artists”的默认名称")]
        public string ArtistsTagTypeName { get; set; }

        [RequiredParameter(typeof(string), "group", alias: "标签类别“Groups”的默认名称")]
        public string GroupsTagTypeName { get; set; }

        [RequiredParameter(typeof(string), "language", alias: "标签类别“Languages”的默认名称")]
        public string LanguagesTagTypeName { get; set; }

        [RequiredParameter(typeof(string), "category", alias: "标签类别“Categories”的默认名称")]
        public string CategoriesTagTypeName { get; set; }

        public async ValueTask ScrapeMetadata(MangaDetail context)
        {
            var html = await Client.GetStringAsync(string.Format(QueryBase, context.Name));
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            var gallery = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'gallery')]");
            var href = gallery?.FirstChild.GetAttributeValue("href", null);
            if (href == null) return;
            var mangaUrl = Host + href;
            html = await Client.GetStringAsync(mangaUrl);
            htmlDoc.LoadHtml(html);
            var titleNode = htmlDoc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'title')]");
            var jpTitleNode = htmlDoc.DocumentNode.SelectSingleNode("//h2[contains(@class, 'title')]");
            context.Aliases ??= new List<string>();
            if(jpTitleNode == null)
            {
                context.Name = titleNode.InnerText;
            }
            else
            {
                context.Name = jpTitleNode.InnerText;
                context.Aliases.Add(titleNode.InnerText);
            }
            context.Tags = new List<TagDetail>();
            foreach (var tagNode in htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'tag-container field-name')]"))
            {
                string tagType;
                switch (tagNode.GetDirectInnerText().Trim())
                {
                    case "Parodies:":
                        tagType = ParodiesTagTypeName;
                        break;
                    case "Characters:":
                        tagType = CharactersTagTypeName;
                        break;
                    case "Tags:":
                        tagType = TagsTagTypeName;
                        break;
                    case "Artists:":
                        tagType = ArtistsTagTypeName;
                        break;
                    case "Groups:":
                        tagType = GroupsTagTypeName;
                        break;
                    case "Languages:":
                        tagType = LanguagesTagTypeName;
                        break;
                    case "Categories:":
                        tagType = CategoriesTagTypeName;
                        break;
                    default: continue;
                }

                context.Tags.AddRange(tagNode.Descendants("span").Where(it => it.GetAttributeValue("class", null) == "name")
                    .Select(tag => new TagDetail() {Name = tag.InnerText, Type = tagType}));
            }
        }
    }
}
