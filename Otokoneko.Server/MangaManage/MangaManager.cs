using Otokoneko.DataType;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using Otokoneko.Server.SearchService;
using Image = Otokoneko.DataType.Image;

namespace Otokoneko.Server.MangaManage
{
    public class MangaManager
    {
        public LibraryManager LibraryManager { get; set; }
        public FtsIndexService FtsIndexService { get; set; }

        private const string DbConnectionString = @"Data Source=./data/manga.db;";
        public Func<string, SqlSugarClient> CreateContext { get; set; }
        private SqlSugarClient Context => CreateContext(DbConnectionString);

        public MangaManager(Func<string, SqlSugarClient> createContext, ILog logger)
        {
            logger.Info("加载漫画索引信息...");
            CreateContext = createContext;
            var context = createContext(DbConnectionString);
            context.CodeFirst.InitTables(
                typeof(Manga),
                typeof(Chapter),
                typeof(Image),
                typeof(MangaTagMapping),
                typeof(Favorite),
                typeof(Comment),
                typeof(ReadProgress),
                typeof(Tag),
                typeof(TagType));
        }

        #region Insert

        public async ValueTask<bool> Upsert(Comment comment)
        {
            using var context = Context;
            try
            {
                comment.CreateTime = comment.UpdateTime = DateTime.UtcNow;
                var commentService = new CommentService(context);
                var result = await commentService.Upsert(comment);
                context.CommitTran();
                return result.ErrorList.Count == 0;
            }
            catch
            {
                context.RollbackTran();
                throw;
            }
        }

        public async ValueTask<bool> Upsert(ReadProgress readProgress)
        {
            using var context = Context;
            try
            {
                context.BeginTran();
                readProgress.ReadTime = DateTime.Now;
                var chapterService = new ChapterService(context);
                readProgress.MangaId = (await chapterService.GetSingleAsync(it => it.ObjectId == readProgress.ChapterId)).MangaId;
                var readProgressService = new ReadProgressService(context);
                var result = await readProgressService.Upsert(readProgress);
                context.CommitTran();
                return true;
            }
            catch (Exception e)
            {
                context.RollbackTran();
                throw;
            }
        }

        public async ValueTask<bool> AddMangaFavorite(long userId, long mangaId)
        {
            using var context = Context;
            try
            {
                context.BeginTran();
                var favoriteService = new FavoriteService(context);
                var favorite = new Favorite
                {
                    EntityId = mangaId,
                    EntityType = EntityType.Manga,
                    UserId = userId,
                    CreateTime = DateTime.Now
                };
                await favoriteService.Insert(favorite);
                context.CommitTran();
                return true;
            }
            catch (Exception e)
            {
                context.RollbackTran();
                throw;
            }
        }

        public async ValueTask<long> Insert(TagType tagType)
        {
            DataFormatter.Format(tagType);
            using var context = Context;
            try
            {
                context.BeginTran();
                var tagTypeService = new TagTypeService(context);
                var success = await tagTypeService.InsertAsync(tagType);
                context.CommitTran();
                return tagType.ObjectId;
            }
            catch (Exception e)
            {
                context.RollbackTran();
                throw;
            }
        }

        public async ValueTask<Tag> Insert(Tag tag)
        {
            if (!DataFormatter.Format(tag)) return null;
            using var context = Context;
            try
            {
                context.BeginTran();
                var tagService = new TagService(context);
                var result = await tagService.Insert(tag);
                if (!result) return null;
                FtsIndexService.CreateFtsIndex(new List<Tag>() { tag });
                context.CommitTran();
                return tag;
            }
            catch (Exception e)
            {
                context.RollbackTran();
                throw;
            }
        }

        public async ValueTask<List<Tag>> Insert(List<Tag> tags)
        {
            if (tags == null || tags.Count <= 0) return null;
            foreach (var tag in tags)
            {
                DataFormatter.Format(tag);
            }
            var context = Context;
            var tagTypeService = new TagTypeService(context);
            var tagService = new TagService(context);
            try
            {
                context.BeginTran();
                await tagTypeService.Insert(tags.Select(it => it.Type).Distinct().ToList());
                var tagType = (await tagTypeService
                        .GetTagTypes(tags.Select(it => it.Type.Name).Distinct().ToList()))
                    .ToDictionary(it => it.Name, it => it);

                foreach (var tag in tags)
                {
                    tag.Type = tagType[tag.Type.Name];
                    tag.TypeId = tag.Type.ObjectId;
                }

                var result = await tagService.Insert(tags);
                FtsIndexService.CreateFtsIndex(result.InsertList.Select(it => it.Item).ToList());
                var realTags = await tagService.GetTags(tags.Select(it => new Tuple<long, string>(it.TypeId, it.Name)).ToList());
                context.CommitTran();
                return realTags;
            }
            catch (Exception e)
            {
                context.RollbackTran();
                throw;
            }
        }

        public async ValueTask<bool> Insert(Manga manga)
        {
            if (!DataFormatter.Format(manga, false, false)) return false;
            using var context = Context;
            try
            {
                context.BeginTran();
                var mangaService = new MangaService(context);
                await mangaService.Upsert(manga);

                var chapterService = new ChapterService(context);
                await chapterService.Upsert(manga.Chapters);

                var imageService = new ImageService(context);
                var images = manga.Chapters.SelectMany(it => it.Images).ToList();
                images.Add(manga.Cover);
                await imageService.Upsert(images);

                if (manga.Tags != null && manga.Tags.Count > 0)
                {
                    var mangaTagMappingService = new MangaTagMappingService(context);
                    var mappings = manga.Tags.Select(it => new MangaTagMapping()
                    { MangaId = manga.ObjectId, TagId = it.Key }).ToList();
                    await mangaTagMappingService.Insert(mappings);
                }

                FtsIndexService.CreateFtsIndex(new List<Manga>() { manga });
                context.CommitTran();
                return true;
            }
            catch (Exception e)
            {
                context.RollbackTran();
                throw;
            }
        }

        #endregion

        #region Update

        public async ValueTask<bool> Update(Manga manga, bool updateChapters)
        {
            if (!DataFormatter.Format(manga, !updateChapters, true)) return false;
            using var context = Context;
            var mangaService = new MangaService(context);
            try
            {
                context.BeginTran();

                if (updateChapters && manga.Chapters != null && manga.Chapters.Count != 0)
                {
                    var chapterService = new ChapterService(context);
                    var imageService = new ImageService(context);
                    var result = await chapterService.Upsert(manga.Chapters);

                    foreach (var insertItem in result.InsertList)
                    {
                        await imageService.Upsert(insertItem.Item.Images);
                    }
                }

                if (manga.Tags != null)
                {
                    var mangaTagMappingService = new MangaTagMappingService(context);
                    await mangaTagMappingService.DeleteTagByMangaId(manga.ObjectId);
                    var mangaTagMappings = manga.Tags.Select(tag => new MangaTagMapping()
                    {
                        MangaId = manga.ObjectId,
                        TagId = tag.Key
                    }).ToList();
                    if (mangaTagMappings.Count > 0)
                        await mangaTagMappingService.Insert(mangaTagMappings);
                }

                await mangaService.Upsert(manga);
                FtsIndexService.UpdateFtsIndex(manga);
                context.CommitTran();
                return true;
            }
            catch (Exception e)
            {
                context.RollbackTran();
                throw;
            }
        }

        public async ValueTask<bool> Update(Tag tag)
        {
            using var context = Context;
            try
            {
                context.BeginTran();
                var tagService = new TagService(context);
                var toBeMapping = new HashSet<long>();
                var mangaTagMappingService = new MangaTagMappingService(context);

                var oldTag = await tagService.GetTagWithAliases(it => it.ObjectId == tag.ObjectId);

                // Update metadata
                if (oldTag == null || !await tagService.Update(tag))
                {
                    context.RollbackTran();
                    return false;
                }

                // Check type of key tag == type of tag
                if (oldTag.Key != tag.Key)
                {
                    var keyTag = await tagService.GetSingleAsync(it => it.ObjectId == tag.Key);
                    if (tag.TypeId != keyTag?.TypeId)
                    {
                        context.RollbackTran();
                        return false;
                    }

                    toBeMapping.Add(oldTag.Key);
                }

                // Recover all removed aliases
                foreach (var alias in oldTag.Aliases.Where(oldTagAlias => tag.Aliases.All(it => it.ObjectId != oldTagAlias.ObjectId)))
                {
                    var oldTagAlias = await tagService.GetSingleAsync(it => it.ObjectId == alias.ObjectId);
                    oldTagAlias.Key = oldTagAlias.ObjectId;
                    if (!await tagService.Update(oldTagAlias))
                    {
                        context.RollbackTran();
                        return false;
                    }
                }

                for (int i = 0; i < tag.Aliases.Count; i++)
                {
                    var tagAlias = tag.Aliases[i];
                    var oldAlias = await tagService.GetSingleAsync(it => it.ObjectId == tagAlias.ObjectId);
                    if (oldAlias.TypeId != tag.TypeId)
                    {
                        context.RollbackTran();
                        return false;
                    }

                    oldAlias.Aliases = await tagService.GetListAsync(it => it.Key == tagAlias.ObjectId && it.Key != it.ObjectId);
                    if (oldAlias.Aliases != null)
                    {
                        tag.Aliases.AddRange(oldAlias.Aliases);
                    }

                    if (oldAlias.Key == tag.Key) continue;
                    toBeMapping.Add(oldAlias.Key);
                    oldAlias.Key = tag.Key;

                    if (await tagService.Update(oldAlias)) continue;
                    context.RollbackTran();
                    return false;
                }

                foreach (var oldKey in toBeMapping)
                {
                    await mangaTagMappingService.Update(oldKey, tag.Key);
                }

                FtsIndexService.UpdateFtsIndex(tag);
                context.CommitTran();
                return true;
            }
            catch (Exception e)
            {
                context.RollbackTran();
                throw;
            }
        }

        #endregion

        #region Query

        public async ValueTask<List<TagType>> GetTagTypes()
        {
            using var context = Context;
            var tagTypeService = new TagTypeService(context);
            return await tagTypeService.GetAllTagTypes();
        }

        public async ValueTask<List<Manga>> GetMangas(List<long> mangaIds, int offset, int limit, OrderType orderType, bool asc)
        {
            using var context = Context;
            var mangaService = new MangaService(context);
            return await mangaService.GetMangas(mangaIds, offset, limit, orderType, asc);
        }

        public async ValueTask<List<Tag>> GetTags(List<long> tagIds, int offset, int limit)
        {
            using var context = Context;
            var tagService = new TagService(context);
            tagIds = tagIds.Skip(offset).Take(limit).ToList();
            return (await tagService
                    .GetListAsync(it => tagIds.Contains(it.ObjectId)))
                .ToList()
                .OrderBy(it => tagIds.IndexOf(it.ObjectId))
                .ToList();
        }

        public async ValueTask<Manga> GetManga(long pathId)
        {
            using var context = Context;
            var mangaService = new MangaService(context);
            var chapterService = new ChapterService(context);
            var mangaTagMappingService = new MangaTagMappingService(context);
            var manga = await mangaService.GetSingleAsync(it => it.PathId == pathId);
            manga.Chapters = await chapterService.GetListAsync(it => it.MangaId == manga.ObjectId);
            manga.Tags = await mangaTagMappingService.GetTagByMangaId(manga.ObjectId);
            return manga;
        }

        public async ValueTask<Manga> GetManga(long mangaId, long userId)
        {
            using var context = Context;
            var mangaService = new MangaService(context);
            var chapterService = new ChapterService(context);
            var mangaTagMappingService = new MangaTagMappingService(context);
            var favoriteService = new FavoriteService(context);
            var readProgressService = new ReadProgressService(context);
            var commentProgressService = new CommentService(context);

            var manga = await mangaService.GetSingleAsync(it => it.ObjectId == mangaId);

            manga.Chapters = (await chapterService
                    .GetListAsync(it => it.MangaId == mangaId))
                .OrderBy(it => it.Order)
                .ToList();

            manga.Tags = await mangaTagMappingService.GetTagByMangaId(mangaId);

            if (userId <= 0) return manga;

            manga.IsFavorite = await favoriteService.IsAnyAsync(it => it.EntityId == mangaId && it.UserId == userId);

            manga.ReadProgresses = await readProgressService.GetListAsync(it => it.MangaId == mangaId && it.UserId == userId);

            manga.Comment = await commentProgressService.GetSingleAsync(it => it.EntityId == mangaId && it.UserId == userId);

            return manga;
        }

        public async ValueTask<List<long>> GetMangaIdByFileTreeNodeId(List<long> fileTreeNodeId)
        {
            using var context = Context;
            var mangaService = new MangaService(context);
            return await mangaService.GetMangaIds(it => fileTreeNodeId.Contains(it.PathId));
        }

        public async ValueTask<Chapter> GetChapter(long chapterId, long userId)
        {
            using var context = Context;
            var chapterService = new ChapterService(context);
            var imageService = new ImageService(context);
            var readProgressService = new ReadProgressService(context);

            var chapter = await chapterService.GetSingleAsync(it => it.ObjectId == chapterId);
            chapter.ReadProgress =
                await readProgressService.GetSingleAsync(it => it.ChapterId == chapterId && it.UserId == userId);
            chapter.Images = await imageService.GetListAsync(it => it.ChapterId == chapterId);

            var uncheckedImage = chapter.Images.Where(it => it.Height == 0).ToList();
            if (uncheckedImage.Count == 0) return chapter;
            foreach (var image in uncheckedImage)
            {
                await using var file = LibraryManager.GeFileTreeNode(image.PathId).OpenRead();
                var (result, width, height) = await ImageUtils.GetMetadata(file);
                file.Close();
                image.Height = height;
                image.Width = width;
            }

            context.BeginTran();
            await imageService.Upsert(uncheckedImage);
            context.CommitTran();
            return chapter;
        }

        public async ValueTask<Image> GetImage(long imageId)
        {
            using var context = Context;
            var imageService = new ImageService(context);
            return await imageService.GetSingleAsync(it => it.ObjectId == imageId);
        }

        public async ValueTask<Tag> GetTag(long tagId)
        {
            using var context = Context;
            var tagService = new TagService(context);
            return await tagService.GetTagWithAliases(it => it.ObjectId == tagId);
        }

        public async ValueTask<ReadProgress> GetReadProgress(long userId, long chapterId)
        {
            using var context = Context;
            var readProgressService = new ReadProgressService(context);
            return await readProgressService.GetSingleAsync(it => it.UserId == userId && it.ChapterId == chapterId);
        }

        public async ValueTask<List<long>> GetSubscribers(long mangaId)
        {
            using var context = Context;
            var favoriteService = new FavoriteService(context);
            return await favoriteService.QueryUserIdByEntityId(mangaId, EntityType.Manga);
        }

        #endregion

        #region Delete

        public async ValueTask<bool> RemoveMangaFavorite(long userId, long mangaId)
        {
            using var context = Context;
            var favoriteService = new FavoriteService(context);
            context.BeginTran();
            try
            {
                if (await favoriteService.DeleteAsync(it => it.EntityId == mangaId && it.UserId == userId))
                {
                    context.CommitTran();
                    return true;
                }
                context.RollbackTran();
                return false;
            }
            catch (Exception e)
            {
                context.RollbackTran();
                throw;
            }
        }

        public async ValueTask<bool> DeleteManga(long mangaId)
        {
            using var context = Context;
            var mangaService = new MangaService(context);
            var chapterService = new ChapterService(context);
            var imageService = new ImageService(context);
            var mangaTagMappingService = new MangaTagMappingService(context);
            var favoriteService = new FavoriteService(context);
            var readProgressService = new ReadProgressService(context);
            context.BeginTran();
            try
            {
                var chapters = await chapterService.GetListAsync(it => it.MangaId == mangaId);
                var chapterIds = chapters.Select(it => it.ObjectId).ToList();
                await mangaService.DeleteAsync(it => it.ObjectId == mangaId);
                await mangaTagMappingService.DeleteAsync(it => it.MangaId == mangaId);
                await chapterService.DeleteAsync(it => it.MangaId == mangaId);
                await imageService.DeleteAsync(it => chapterIds.Contains(it.ChapterId));
                await favoriteService.DeleteAsync(it => it.EntityId == mangaId);
                await readProgressService.DeleteAsync(it => it.MangaId == mangaId);
                context.CommitTran();
                FtsIndexService.DeleteMangaFtxIndex(mangaId);
                return true;
            }
            catch (Exception e)
            {
                context.RollbackTran();
                throw;
            }
        }

        public async ValueTask<bool> DeleteTag(long tagId)
        {
            using var context = Context;
            var mangaTagMappingService = new MangaTagMappingService(context);
            var tagService = new TagService(context);
            context.BeginTran();
            try
            {
                context.BeginTran();
                var tag = await tagService.GetSingleAsync(it => it.ObjectId == tagId);

                if (tag.Key == tag.ObjectId)
                {
                    var keyTagList = await tagService.GetListAsync(it => it.Key == tagId && it.ObjectId != tagId);
                    if (keyTagList.Count != 0)
                    {
                        var newKey = keyTagList.First().ObjectId;
                        await tagService.UpdateKey(tagId, newKey);
                        await mangaTagMappingService.Update(tagId, newKey);
                    }
                    else
                    {
                        await mangaTagMappingService.DeleteAsync(it => it.TagId == tagId);
                    }
                }

                await tagService.DeleteAsync(it => it.ObjectId == tagId);
                context.CommitTran();
                FtsIndexService.DeleteTagFtxIndex(tagId);
                return true;
            }
            catch (Exception e)
            {
                context.RollbackTran();
                throw;
            }
        }

        public async ValueTask<bool> DeleteTagType(long tagTypeId)
        {
            using var context = Context;
            var mangaTagMappingService = new MangaTagMappingService(context);
            var tagService = new TagService(context);
            var tagTypeService = new TagTypeService(context);
            try
            {
                context.BeginTran();

                var tags = await tagService.GetListAsync(it => it.TypeId == tagTypeId);
                var keys = tags.Select(it => it.Key).ToList();
                await mangaTagMappingService.DeleteAsync(it => keys.Contains(it.TagId));
                await tagService.DeleteAsync(it => it.TypeId == tagTypeId);
                await tagTypeService.DeleteAsync(it => it.ObjectId == tagTypeId);

                context.CommitTran();
                return true;
            }
            catch (Exception e)
            {
                context.RollbackTran();
                throw;
            }
        }

        #endregion
    }
}