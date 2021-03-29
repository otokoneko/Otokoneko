using System;
using IdGen;
using Otokoneko.DataType;

namespace Otokoneko.Server.MangaManage
{
    public static class DataFormatter
    {
        private static readonly IdGenerator IdGenerator =
            new IdGenerator(1, new IdGeneratorOptions(new IdStructure(41, 6, 16)));

        public static bool Format(Manga manga, bool ignoreChapter, bool ignoreCover)
        {
            if (manga.ObjectId <= 0) manga.ObjectId = IdGenerator.CreateId();
            if (!ignoreChapter)
            {
                if (manga.Chapters == null) return false;
                var order = 0;
                foreach (var chapter in manga.Chapters)
                {
                    chapter.MangaId = manga.ObjectId;
                    chapter.Order = order++;
                    if (!Format(chapter))
                    {
                        return false;
                    }
                }

            }

            if (!ignoreCover)
            {
                if (manga.Cover == null) return false;
                Format(manga.Cover);
                manga.Cover.ChapterId = manga.ObjectId;
                manga.CoverId = manga.Cover.ObjectId;
            }

            if (manga.Path != null) manga.PathId = manga.Path.ObjectId;
            manga.CreateTime = manga.UpdateTime = DateTime.UtcNow;
            return true;
        }

        public static bool Format(Chapter chapter)
        {
            if (chapter.ObjectId <= 0) chapter.ObjectId = IdGenerator.CreateId();
            else return true; // 已完成格式化

            if (chapter.Images == null) return false;
            var order = 0;
            foreach (var image in chapter.Images)
            {
                image.ChapterId = chapter.ObjectId;
                image.Order = order++;
                if (!Format(image))
                {
                    return false;
                }
            }

            if (chapter.Path != null) chapter.PathId = chapter.Path.ObjectId;
            chapter.CreateTime = chapter.UpdateTime = DateTime.UtcNow;
            return true;
        }

        public static bool Format(Image image)
        {
            if (image.ObjectId <= 0) image.ObjectId = IdGenerator.CreateId();
            if (image.Path != null) image.PathId = image.Path.ObjectId;
            return true;
        }

        public static bool Format(Tag tag)
        {
            if (tag.ObjectId <= 0) tag.Key = tag.ObjectId = IdGenerator.CreateId();
            tag.Name = tag.Name.Trim();
            return (tag.TypeId > 0) || (tag.Type != null && Format(tag.Type));
        }

        public static bool Format(TagType tagType)
        {
            if (tagType.ObjectId <= 0) tagType.ObjectId = IdGenerator.CreateId();
            return true;
        }
    }
}
