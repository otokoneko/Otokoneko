using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Otokoneko.Server.MangaManage;
using SqlSugar;

namespace Otokoneko.Server.SearchService
{
    public class TagKeywordSearchService
    {
        public TagFtsIndexService TagFtsIndexService { get; set; }
        private const string DbConnectionString = @"Data Source=./data/manga.db;";
        public Func<string, SqlSugarClient> CreateContext { get; set; }
        private SqlSugarClient Context => CreateContext(DbConnectionString);

        public async ValueTask<List<long>> Search(string queryString, long typeId)
        {
            using var context = Context;
            var tagService = new TagService(context);
            if (string.IsNullOrEmpty(queryString?.Trim()))
            {
                return await (typeId <= -1
                    ? tagService.GetTagIds(it => it.Key == it.ObjectId)
                    : tagService.GetTagIds(it => it.Key == it.ObjectId && it.TypeId == typeId));
            }
            else
            {
                var ftsResult = TagFtsIndexService.Search(queryString);
                var result = await tagService.GetTagKeys(it => ftsResult.Contains(it.ObjectId));
                if (typeId > 0) result = await tagService.GetTagIds(it => result.Contains(it.ObjectId) && it.TypeId == typeId);
                return result.OrderBy(it => ftsResult.IndexOf(it)).ToList();
            }
        }
    }
}
