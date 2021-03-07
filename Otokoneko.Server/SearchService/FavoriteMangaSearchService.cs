using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Otokoneko.Server.MangaManage;
using SqlSugar;

namespace Otokoneko.Server.SearchService
{
    public class FavoriteMangaSearchService
    {
        private const string DbConnectionString = @"Data Source=./data/manga.db;";
        public Func<string, SqlSugarClient> CreateContext { get; set; }
        private SqlSugarClient Context => CreateContext(DbConnectionString);

        public ValueTask<List<long>> Search(long userId)
        {
            using var context = Context;
            var favoriteService = new FavoriteService(context);
            return favoriteService.QueryEntityIdsByUserId(userId);
        }
    }
}
