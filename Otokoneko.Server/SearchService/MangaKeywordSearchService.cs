using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Otokoneko.Server.MangaManage;
using SqlSugar;
using Otokoneko.DataType;
using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;

namespace Otokoneko.Server.SearchService
{
    public interface ISearchToken
    {
        public ISearchToken Parent { get; set; }
        public ISearchToken[] Children { get; set; }
    }

    public class OrArray : ISearchToken
    {
        public ISearchToken Parent { get; set; }
        public ISearchToken[] Children { get; set; }
        
        public OrArray(ISearchToken[] children)
        {
            Children = children;
            foreach (var child in children)
            {
                child.Parent = this;
            }
        }
    }

    public class AndArray : ISearchToken
    {
        public ISearchToken Parent { get; set; }
        public ISearchToken[] Children { get; set; }

        public AndArray(ISearchToken[] children)
        {
            Children = children;
            foreach (var child in children)
            {
                child.Parent = this;
            }
        }
    }

    public class TagToken : ISearchToken
    {
        public string Type { get; }
        public string TagName { get; }
        public bool Exclude { get; }
        public Tag Tag { get; set; }
        public ISearchToken Parent { get; set; }
        public ISearchToken[] Children { get; set; }

        public TagToken(string type, string tagName, bool exclude)
        {
            Type = type;
            TagName = tagName;
            Exclude = exclude;
        }

        public TagToken(Tag tag, bool exclude)
        {
            Tag = tag;
            Exclude = exclude;
        }
    }

    public class KeywordToken : ISearchToken
    {
        public string Keyword { get; }
        public ISearchToken Parent { get; set; }
        public ISearchToken[] Children { get; set; }

        public KeywordToken(string keyword)
        {
            Keyword = keyword;
        }
    }

    public class MangaKeywordSearchService
    {
        public MangaFtsIndexService MangaFtsIndexService { get; set; }
        private const string DbConnectionString = @"Data Source=./data/manga.db;";
        public Func<string, SqlSugarClient> CreateContext { get; set; }
        private SqlSugarClient Context => CreateContext(DbConnectionString);

        #region SearchKerwordParser

        private static readonly Parser<char, char> Tilde = Char('~');
        private static readonly Parser<char, char> TagQuote = Char('$');
        private static readonly Parser<char, char> LParen = Char('(');
        private static readonly Parser<char, char> RParen = Char(')');
        private static readonly Parser<char, char> Exclude = Char('-');

        private static readonly Parser<char, string> String =
            Token(c => c != '$' && c != '(')
                .AtLeastOnceString();

        private static readonly Parser<char, ISearchToken> Keyword =
                String
                .Select(it => (ISearchToken)new KeywordToken(it));

        private static readonly Parser<char, ISearchToken> IncludeTag =
            String
                .Between(TagQuote)
                .Between(SkipWhitespaces)
                .Select(it => {
                    var array = it.Split(':');
                    return array.Length > 1
                        ? new TagToken(array[0], string.Join(null, array.Skip(1).ToArray()), false)
                        : (ISearchToken)new TagToken(null, it, false);
                });

        private static readonly Parser<char, ISearchToken> ExcludeTag =
            String
                .Between(TagQuote)
                .Between(Exclude, SkipWhitespaces)
                .Select(it => {
                    var array = it.Split(':');
                    return array.Length > 1
                        ? new TagToken(array[0], string.Join(null, array.Skip(1).ToArray()), false)
                        : (ISearchToken)new TagToken(null, it, true);
                });

        private static readonly Parser<char, ISearchToken> Atom =
            ExcludeTag
                .Or(IncludeTag)
                .Or(Keyword);

        private static readonly Parser<char, ISearchToken> OrArrayParser =
            IncludeTag
                .Separated(Tilde)
                .Between(LParen, RParen)
                .Select(it => (ISearchToken)new OrArray(it.ToArray()));

        private static readonly Parser<char, ISearchToken> AndArrayElementParser =
            OrArrayParser
                .Or(Atom);

        private static readonly Parser<char, ISearchToken> AndArrayParser =
            AndArrayElementParser
                .Separated(SkipWhitespaces)
                .Select(it => (ISearchToken)new AndArray(it.ToArray()));

        #endregion

        private void Simplify(ISearchToken token)
        {
            if (token is AndArray andArray)
            {
                var children = andArray.Children.ToList();
                for (var i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    child.Parent = andArray;
                    Simplify(child);
                    if (!(child is AndArray)) continue;
                    children.AddRange(child.Children);
                    children.RemoveAt(i);
                    i--;
                }

                token.Children = children.ToArray();
            }
            else if(token is OrArray orArray)
            {
                var children = orArray.Children.ToList();
                for (var i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    child.Parent = orArray;
                    Simplify(child);
                    if (!(child is OrArray)) continue;
                    children.AddRange(child.Children);
                    children.RemoveAt(i);
                    i--;
                }

                token.Children = children.ToArray();
            }
        }

        private void GetTagTokens(ISearchToken token, HashSet<Tuple<string, string>> tagTypeNameAndTagNames, List<TagToken> tagTokens)
        {
            foreach (var searchToken in token.Children)
            {
                if (searchToken is TagToken tagToken)
                {
                    tagTypeNameAndTagNames.Add(new Tuple<string, string>(tagToken.Type, tagToken.TagName));
                    tagTokens.Add(tagToken);
                }
                else if (searchToken is OrArray orArray)
                {
                    GetTagTokens(orArray, tagTypeNameAndTagNames, tagTokens);
                }
            }
        }

        private async ValueTask<ISearchToken> ParseQueryString(string queryString, TagService tagService)
        {
            var result = AndArrayParser.Parse(queryString);
            if (!result.Success) return null;
            var token = result.Value;
            var tagTypeNameAndTagNames = new HashSet<Tuple<string, string>>();
            var tagTokens = new List<TagToken>();
            GetTagTokens(token, tagTypeNameAndTagNames, tagTokens);

            if (tagTypeNameAndTagNames.Count != 0)
            {
                var mapping = await tagService.GetTags(tagTypeNameAndTagNames.ToList());
                foreach (var tagToken in tagTokens)
                {
                    if (!mapping.TryGetValue(new Tuple<string, string>(tagToken.Type, tagToken.TagName),
                        out var tags)) continue;
                    if (tags.Count <= 1)
                    {
                        tagToken.Tag = tags.FirstOrDefault();
                    }
                    else
                    {
                        var children = tagToken.Parent.Children;
                        for (var i = 0; i < children.Length; i++)
                        {
                            if (children[i] != tagToken) continue;
                            if (tagToken.Exclude)
                            {
                                children[i] = new AndArray(tags
                                    .Select(it => new TagToken(it, true) { Parent = tagToken.Parent }).ToArray());
                            }
                            else
                            {
                                children[i] = new OrArray(tags
                                    .Select(it => new TagToken(it, false) { Parent = tagToken.Parent }).ToArray());
                            }
                            break;
                        }
                    }
                }
            }

            Simplify(token);
            return token;
        }

        public async ValueTask<List<long>> Search(string queryString)
        {
            using var context = Context;
            var mangaService = new MangaService(context);
            var mangaTagMappingService = new MangaTagMappingService(context);
            var tagService = new TagService(context);

            var token = await ParseQueryString(queryString, tagService);
            if (token == null) return new List<long>();

            List<long> result = await mangaService.GetAllMangaIds();
            List<long> ftsResult = null;

            var keywords = token.Children.OfType<KeywordToken>().Select(it=>it.Keyword).ToList();
            var tagTokens = token.Children.OfType<TagToken>().ToList();
            var includeTags = tagTokens.Where(it => !it.Exclude).Select(it=>it.Tag).ToList();
            var excludeTags = tagTokens.Where(it => it.Exclude).Select(it => it.Tag).ToList();
            var orArray = token.Children.OfType<OrArray>().ToList();

            if (keywords.Count != 0)
            {
                ftsResult = MangaFtsIndexService.Search(string.Join(' ', keywords));
                result = result.Intersect(ftsResult).ToList();
                if (result.Count == 0) return result;
            }

            if (includeTags.Count != 0)
            {
                if (includeTags.Any(it => it == null)) return new List<long>();
                var includeTagSearchResult = await mangaTagMappingService.GetMangasWithAllTags(includeTags.Select(it=>it.Key).Distinct().ToList());
                result = result.Intersect(includeTagSearchResult).ToList();
                if (result.Count == 0) return result;
            }

            if (excludeTags.Count != 0)
            {
                var excludeTagSearchResult = await mangaTagMappingService.GetMangasWithoutAnyTags(excludeTags.Select(it => it.Key).Distinct().ToList());
                result = result.Intersect(excludeTagSearchResult).ToList();
                if (result.Count == 0) return result;
            }

            foreach (var orToken in orArray)
            {
                result = result.Intersect(
                    await mangaTagMappingService.GetMangasWithAnyTags(orToken.Children.OfType<TagToken>()
                        .Where(it=>it.Tag!=null)
                        .Select(it => it.Tag.Key).ToList())).ToList();
                if (result.Count == 0) return result;
            }

            if (ftsResult != null)
            {
                result = result.OrderBy(it => ftsResult.IndexOf(it)).ToList();
            }

            return result;
        }
    }
}
