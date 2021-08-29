using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Otokoneko.DataType;
using SqlSugar;

namespace Otokoneko.Server.MangaManage
{
    public class MangaService : SimpleClient<Manga>
    {
        public MangaService(ISqlSugarClient context) : base(context){}

        public async ValueTask<Manga> GetMangaWithChapters(long objectId)
        {
            return await Context.Queryable<Manga>()
                .Where(it => it.ObjectId == objectId)
                .Mapper(it => it.Chapters,
                    it => it.Chapters.First().MangaId,
                    it => it.ObjectId)
                .SingleAsync();
        }

        public async ValueTask<List<long>> GetMangaIds(Expression<Func<Manga, bool>> whereExpression)
        {
            return await Context.Queryable<Manga>().Where(whereExpression).Select(it => it.ObjectId).ToListAsync();
        }

        public async ValueTask<List<long>> GetAllMangaIds()
        {
            return await Context.Queryable<Manga>().Select(it => it.ObjectId).ToListAsync();
        }

        public async ValueTask<List<Manga>> GetMangas(List<long> objectIds, int offset, int limit, OrderType orderType, bool asc)
        {
            if (orderType == OrderType.Default)
            {
                return (await Context.Queryable<Manga>()
                        .Where(it => objectIds.Contains(it.ObjectId))
                        .ToListAsync())
                    .OrderBy(it => objectIds.IndexOf(it.ObjectId))
                    .Skip(offset)
                    .Take(limit)
                    .ToList();
            }
            else
            {
                return await Context.Queryable<Manga>().Where(it => objectIds.Contains(it.ObjectId))
                    .OrderBy(it => it.UpdateTime,
                        asc 
                            ? OrderByType.Asc 
                            : OrderByType.Desc)
                    .Skip(offset)
                    .Take(limit)
                    .ToListAsync();
            }
        }

        public async ValueTask<StorageableResult<Manga>> Upsert(Manga manga)
        {
            var result = Context.Storageable(manga)
                .SplitUpdate(it => it.Any(m => m.ObjectId == it.Item.ObjectId))
                .SplitInsert(it => true)
                .ToStorage();

            await result
                .AsUpdateable
                .UpdateColumns(it => new { it.Title, it.Description, it.UpdateTime, it.Aliases, it.CoverId })
                .ExecuteCommandAsync();

            await result
                .AsInsertable
                .ExecuteCommandAsync();
            
            return result;
        }
    }

    public class ChapterService : SimpleClient<Chapter>
    {
        public ChapterService(ISqlSugarClient context) : base(context) { }

        public async ValueTask<Chapter> GetChapterWithImages(long objectId)
        {
            return await Context.Queryable<Chapter>()
                .Where(it => it.ObjectId == objectId)
                .Mapper(it => it.Images,
                    it => it.Images.First().ChapterId,
                    it => it.ObjectId)
                .SingleAsync();
        }

        public async ValueTask<StorageableResult<Chapter>> Upsert(List<Chapter> chapters)
        {
            var result = Context.Storageable(chapters)
                .SplitUpdate(it => it.Any(c => c.ObjectId == it.Item.ObjectId))
                .SplitInsert(it => true)
                .ToStorage();
            await result
                .AsUpdateable
                .UpdateColumns(it => new {it.Title, it.ChapterClass, it.Order, it.UpdateTime})
                .ExecuteCommandAsync();
            await result
                .AsInsertable
                .ExecuteCommandAsync();
            return result;
        }
    }

    public class ImageService : SimpleClient<Image>
    {
        public ImageService(ISqlSugarClient context) : base(context) { }

        public async ValueTask<StorageableResult<Image>> Upsert(List<Image> images)
        {
            var result = Context.Storageable(images)
                .SplitUpdate(it => it.Any(c => c.ObjectId == it.Item.ObjectId))
                .SplitInsert(it => true)
                .ToStorage();
            await result
                .AsUpdateable
                .ExecuteCommandAsync();
            await result
                .AsInsertable
                .ExecuteCommandAsync();
            return result;
        }
    }

    public class TagService : SimpleClient<Tag>
    {
        public TagService(ISqlSugarClient context) : base(context) { }

        public async ValueTask<List<long>> GetTagIds(Expression<Func<Tag, bool>> whereExpression)
        {
            return await Context
                .Queryable<Tag>()
                .Where(whereExpression)
                .OrderBy(it => new {it.TypeId, it.Name})
                .Select(it => it.ObjectId)
                .ToListAsync();
        }

        public async ValueTask<List<long>> GetTagKeys(Expression<Func<Tag, bool>> whereExpression)
        {
            return await Context
                .Queryable<Tag>()
                .Where(whereExpression)
                .Select(it => it.Key)
                .Distinct()
                .ToListAsync();
        }

        public async ValueTask<List<Tag>> GetTags(List<Tuple<long, string>> typeIdAndNames)
        {
            var result = await Context
                .Queryable<Tag>()
                .Where(it => typeIdAndNames.Select(t => t.Item2).ToList().Contains(it.Name))
                .ToListAsync();
            result = result.Where(it => typeIdAndNames.Contains(new Tuple<long, string>(it.TypeId, it.Name))).ToList();
            return result;
        }

        public async ValueTask<Dictionary<Tuple<string, string>, List<Tag>>> GetTags(List<Tuple<string, string>> tagTypeNameAndTagNames)
        {
            var typeNames = tagTypeNameAndTagNames.Select(it => it.Item1).ToList();
            var types = (await Context.Queryable<TagType>()
                .Where(it => typeNames.Contains(it.Name))
                .Distinct()
                .ToListAsync());

            var typeIdDict = types
                .ToDictionary(it => it.Name, it => it.ObjectId);

            var tagNames = tagTypeNameAndTagNames.Select(it => it.Item2).ToList();
            var tags = await Context.Queryable<Tag>()
                .Where(it => tagNames.Contains(it.Name))
                .Distinct()
                .ToListAsync();

            return tagTypeNameAndTagNames.ToDictionary(key => key, key => key.Item1 == null
                ? tags.Where(it => it.Name == key.Item2).ToList()
                : tags.Where(it => it.Name == key.Item2 && it.TypeId == typeIdDict[key.Item1]).ToList());
        }

        public async ValueTask<StorageableResult<Tag>> Insert(List<Tag> tags)
        {
            var result = Context
                .Storageable(tags)
                .SplitIgnore(it => it.Any(t => t.Name == it.Item.Name && t.TypeId == it.Item.TypeId))
                .SplitInsert(it => true)
                .WhereColumns(it => new { it.Name, it.TypeId })
                .ToStorage();
            await result.AsInsertable.ExecuteCommandAsync();
            return result;
        }

        public async ValueTask<bool> UpdateKey(long oldKey, long newKey)
        {
            await Context.Updateable<Tag>()
                .SetColumns(it => it.Key == newKey)
                .Where(it => it.Key == oldKey)
                .ExecuteCommandAsync();
            return true;
        }

        public new async ValueTask<bool> Update(Tag tag)
        {
            if (await Context.Queryable<Tag>().AnyAsync(it => it.Name==tag.Name&&it.TypeId==tag.TypeId&&it.ObjectId!=tag.ObjectId))
            {
                return false;
            }
            var result = Context.Storageable(tag)
                .SplitUpdate(it => it.Any(t => t.ObjectId == it.Item.ObjectId))
                .SplitInsert(it => true)
                .WhereColumns(it => new {it.ObjectId})
                .ToStorage();
            if (result.UpdateList.Count != 1) return false;
            await result.AsUpdateable.ExecuteCommandAsync();
            return true;
        }

        public new async ValueTask<bool> Insert(Tag tag)
        {
            var result = Context.Storageable(tag)
                .SplitIgnore(it => it.Any(t => t.Name == it.Item.Name && t.TypeId == it.Item.TypeId))
                .SplitInsert(it => true)
                .WhereColumns(it => new { it.Name, it.TypeId })
                .ToStorage();
            await result.AsInsertable.ExecuteCommandAsync();
            return result.InsertList.Count == 1;
        }

        public async ValueTask<Tag> GetTagWithAliases(Expression<Func<Tag, bool>> whereExpression)
        {
            var tag = await Context.Queryable<Tag>()
                .Where(whereExpression)
                .SingleAsync();
            if (tag == null) return null;
            tag.Aliases = await Context.Queryable<Tag>()
                .Where(it => it.Key == tag.ObjectId && it.Key != it.ObjectId)
                .ToListAsync();
            return tag;
        }
    }

    public class TagTypeService : SimpleClient<TagType>
    {
        public TagTypeService(ISqlSugarClient context) : base(context){}

        public async ValueTask<List<TagType>> GetAllTagTypes()
        {
            return await Context.Queryable<TagType>().ToListAsync();
        }

        public async ValueTask<List<TagType>> GetTagTypes(List<string> names)
        {
            return await Context
                .Queryable<TagType>()
                .Where(it => names.Contains(it.Name))
                .ToListAsync();
        }

        public async ValueTask<StorageableResult<TagType>> Insert(List<TagType> tagTypes)
        {
            var result = Context
                .Storageable(tagTypes)
                .SplitIgnore(it => it.Any(tt => tt.Name == it.Item.Name))
                .SplitInsert(it => true)
                .WhereColumns(it => it.Name)
                .ToStorage();
            await result.AsInsertable.ExecuteCommandAsync();
            return result;
        }
    }

    public class CommentService : SimpleClient<Comment>
    {
        public CommentService(ISqlSugarClient context) : base(context) { }

        public async ValueTask<StorageableResult<Comment>> Upsert(Comment comment)
        {
            var result = Context.Storageable(comment)
                .SplitUpdate(it => it.Any(c => c.EntityId == it.Item.EntityId && c.UserId == it.Item.UserId))
                .SplitInsert(it => true)
                .WhereColumns(it => new { it.EntityId, it.UserId })
                .ToStorage();
            await result
                .AsUpdateable
                .IgnoreColumns(it => it.CreateTime)
                .ExecuteCommandAsync();
            await result
                .AsInsertable
                .ExecuteCommandAsync();
            return result;
        }
    }

    public class MangaTagMappingService : SimpleClient<MangaTagMapping>
    {
        public MangaTagMappingService(ISqlSugarClient context) : base(context) { }

        public async ValueTask<List<long>> GetMangasWithAllTags(List<long> tagIds)
        {
            return await Context.Queryable<MangaTagMapping>()
                .Where(it => tagIds.Contains(it.TagId))
                .GroupBy(it => it.MangaId)
                .Having(it => SqlFunc.AggregateDistinctCount(it.TagId) == tagIds.Count)
                .Select(it => it.MangaId)
                .Distinct()
                .ToListAsync();
        }

        public async ValueTask<List<long>> GetMangasWithAnyTags(List<long> tagIds)
        {
            return await Context.Queryable<MangaTagMapping>()
                .Where(it => tagIds.Contains(it.TagId))
                .Select(it => it.MangaId)
                .Distinct()
                .ToListAsync();
        }

        public async ValueTask<List<long>> GetMangasWithoutAnyTags(List<long> tagIds)
        {
            var mangaIds = await Context.Queryable<Manga>()
                .Where(it => SqlFunc.Subqueryable<MangaTagMapping>()
                    .Where(mp => tagIds.Contains(mp.TagId))
                    .Where(mp => mp.MangaId == it.ObjectId)
                    .NotAny())
                .Select(it => it.ObjectId)
                .Distinct()
                .ToListAsync();
            return mangaIds;
        }

        public async ValueTask<StorageableResult<MangaTagMapping>> Insert(List<MangaTagMapping> mappings)
        {
            var result = Context.Storageable(mappings)
                .SplitIgnore(it => it.Any(m => m.MangaId == it.Item.MangaId && m.TagId == it.Item.TagId))
                .SplitInsert(it => true)
                .WhereColumns(it => new {it.MangaId, it.TagId})
                .ToStorage();
            await result.AsInsertable.ExecuteCommandAsync();
            return result;
        }

        public async ValueTask<bool> Update(long oldKey, long newKey)
        {
            await Context.Updateable<MangaTagMapping>()
                .SetColumns(it => it.TagId == newKey)
                .Where(it => it.TagId == oldKey)
                .ExecuteCommandAsync();
            return true;
        }

        public async ValueTask<List<Tag>> GetTagByMangaId(long mangaId)
        {
            return (await Context.Queryable<MangaTagMapping>()
                    .Where(it => it.MangaId == mangaId)
                    .Mapper(it => it.Tag, it => it.Tag.ObjectId, it => it.TagId)
                    .ToListAsync())
                .Select(it => it.Tag)
                .ToList();
        }

        public async ValueTask<bool> DeleteTagByMangaId(long mangaId)
        {
            return await DeleteAsync(it => it.MangaId == mangaId);
        }
    }

    public class ReadProgressService : SimpleClient<ReadProgress>
    {
        public ReadProgressService(ISqlSugarClient context) : base(context) { }

        public async ValueTask<StorageableResult<ReadProgress>> Upsert(ReadProgress readProgress)
        {
            var result = Context.Storageable(readProgress)
                .SplitUpdate(it => it.Any(rp => rp.UserId == it.Item.UserId && rp.ChapterId == it.Item.ChapterId))
                .SplitInsert(it => true)
                .WhereColumns(it => new { it.UserId, it.ChapterId })
                .ToStorage();
            await result.AsInsertable.ExecuteCommandAsync();
            await result.AsUpdateable.ExecuteCommandAsync();
            return result;
        }

        public async ValueTask<List<long>> GetMangaIdsByUserId(long userId)
        {
            return await Context
                .Queryable<ReadProgress>()
                .Where(it => it.UserId == userId)
                .OrderBy(it => it.ReadTime, OrderByType.Desc)
                .Select(it => it.MangaId)
                .Distinct()
                .ToListAsync();
        }
    }

    public class FavoriteService : SimpleClient<Favorite>
    {
        public FavoriteService(ISqlSugarClient context) : base(context) { }

        public new async ValueTask<StorageableResult<Favorite>> Insert(Favorite favorite)
        {
            var result = Context.Storageable(favorite)
                .SplitIgnore(it => it.Any(rp => rp.UserId == it.Item.UserId && rp.EntityId == it.Item.EntityId))
                .SplitInsert(it => true)
                .WhereColumns(it => new { it.UserId, it.EntityId })
                .ToStorage();
            await result.AsInsertable.ExecuteCommandAsync();
            return result;
        }

        public async ValueTask<List<long>> QueryEntityIdsByUserId(long userId)
        {
            return await Context
                .Queryable<Favorite>()
                .Where(it => it.UserId == userId)
                .Select(it => it.EntityId)
                .Distinct()
                .ToListAsync();
        }

        public async ValueTask<List<long>> QueryUserIdByEntityId(long entityId, EntityType entityType)
        {
            return await Context
                .Queryable<Favorite>()
                .Where(it => it.EntityId == entityId && it.EntityType == entityType)
                .Select(it => it.UserId)
                .ToListAsync();
        }
    }
}