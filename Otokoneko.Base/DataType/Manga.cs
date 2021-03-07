using MessagePack;
using System;
using System.Collections.Generic;
#if SERVER
using SqlSugar;
#endif

namespace Otokoneko.DataType
{
    public enum EntityType
    {
        Manga,
        Chapter,
        Image,
        Tag
    }


    [MessagePackObject]
    public partial class Manga
    {
#if SERVER
        [SugarColumn(IsPrimaryKey = true)]
#endif
        [Key(0)]
        public long ObjectId { get; set; }
        [Key(1)]
        public long CoverId { get; set; }
        [Key(2)]
        public string Title { get; set; }
        [Key(3)]
        public DateTime CreateTime { get; set; }
        [Key(4)]
        public DateTime UpdateTime { get; set; }
#if SERVER
        [SugarColumn(IsNullable = true)]
#endif
        [Key(5)]
        public string Description { get; set; }
#if SERVER
        [SugarColumn(IsIgnore = true)]
#endif
        [Key(6)]
        public List<Tag> Tags { get; set; }
#if SERVER
        [SugarColumn(IsIgnore = true)]
#endif
        [Key(7)]
        public List<Chapter> Chapters { get; set; }
#if SERVER
        [SugarColumn(IsIgnore = true)]
#endif
        [Key(8)]
        public bool IsFavorite { get; set; }
#if SERVER
        [SugarColumn(IsIgnore = true)]
#endif
        [Key(9)]
        public List<ReadProgress> ReadProgresses { get; set; }
#if SERVER
        [SugarColumn(IsNullable = true)]
#endif
        [Key(10)]
        public string Aliases { get; set; }
#if SERVER
        [SugarColumn(IsIgnore = true)]
#endif
        [Key(11)]
        public Comment Comment { get; set; }
        [Key(12)]
        public int Version { get; set; }
    }

    [MessagePackObject]
    public partial class Chapter
    {
#if SERVER
        [SugarColumn(IsPrimaryKey = true)]
#endif
        [Key(0)]
        public long ObjectId { get; set; }
        [Key(1)]
        public int Order { get; set; }
        [Key(2)]
        public string Title { get; set; }
        [Key(3)]
        public DateTime CreateTime { get; set; }
        [Key(4)]
        public DateTime UpdateTime { get; set; }
        [Key(5)]
        public string ChapterClass { get; set; }
#if SERVER
        [SugarColumn(IsIgnore = true)]
#endif
        [Key(6)]
        public List<Image> Images { get; set; }
#if SERVER
        [SugarColumn(IsIgnore = true)]
#endif
        [Key(7)]
        public ReadProgress ReadProgress { get; set; }
    }

    [MessagePackObject]
    public partial class Image
    {
#if SERVER
        [SugarColumn(IsPrimaryKey = true)]
#endif
        [Key(0)]
        public long ObjectId { get; set; }
        [Key(1)]
        public int Order { get; set; }
        [Key(2)]
        public int Height { get; set; }
        [Key(3)]
        public int Width { get; set; }
    }

    [MessagePackObject]
    public partial class TagType
    {
#if SERVER
        [SugarColumn(IsPrimaryKey = true)]
#endif
        [Key(0)]
        public long ObjectId { get; set; }
#if SERVER
        [SugarColumn(UniqueGroupNameList = new[] { nameof(Name) })]
#endif
        [Key(1)]
        public string Name { get; set; }
    }

    [MessagePackObject]
    public partial class Tag
    {
#if SERVER
        [SugarColumn(IsPrimaryKey = true)]
#endif
        [Key(0)]
        public long ObjectId { get; set; }
#if SERVER
        [SugarColumn(UniqueGroupNameList = new[] { nameof(Name), nameof(TypeId) })]
#endif
        [Key(1)]
        public long TypeId { get; set; }
        [Key(3)]
#if SERVER
        [SugarColumn(IsNullable = true)]
#endif
        public string Detail { get; set; }
#if SERVER
        [SugarColumn(UniqueGroupNameList = new[] { nameof(Name), nameof(TypeId) })]
#endif
        [Key(2)]
        public string Name { get; set; }
        [Key(4)]
        public long Key { get; set; }
#if SERVER
        [SugarColumn(IsIgnore = true)]
#endif
        [Key(5)]
        public List<Tag> Aliases { get; set; }
        [Key(6)]
        public int Version { get; set; }

        // public override bool Equals(object? obj)
        // {
        //     if (!(obj is TagType type)) return false;
        //     return Name == type.Name;
        // }
    }

    [MessagePackObject]
    public class ReadProgress
    {
#if SERVER
        [SugarColumn(UniqueGroupNameList = new[] { nameof(ChapterId), nameof(UserId) })]
#endif
        [Key(0)]
        public long ChapterId { get; set; }
        [Key(1)]
        public int Progress { get; set; }
        [Key(2)]
        public DateTime ReadTime { get; set; }
#if SERVER
        [SugarColumn(UniqueGroupNameList = new[] { nameof(ChapterId), nameof(UserId) })]
#endif
        [IgnoreMember]
        public long UserId { get; set; }
        [IgnoreMember]
        public long MangaId { get; set; }
        [Key(3)]
        public int Version { get; set; }
#if SERVER
        [SugarColumn(IsIgnore = true)]
#endif
        [IgnoreMember]
        public Manga Manga { get; set; }
#if SERVER
        [SugarColumn(IsIgnore = true)]
#endif
        [IgnoreMember]
        public Chapter Chapter { get; set; }
    }

    [MessagePackObject]
    public class Comment
    {
#if SERVER
        [SugarColumn(UniqueGroupNameList = new[] { nameof(EntityId), nameof(UserId) })]
#endif
        [Key(0)]
        public long UserId { get; set; }
#if SERVER
        [SugarColumn(UniqueGroupNameList = new[] { nameof(EntityId), nameof(UserId) })]
#endif
        [Key(1)]
        public long EntityId { get; set; }
        [Key(2)]
        public EntityType EntityType { get; set; }
        [Key(3)]
        public int Score { get; set; }
        [Key(4)]
        public string Text { get; set; }
        [Key(5)]
        public DateTime CreateTime { get; set; }
        [Key(6)]
        public DateTime UpdateTime { get; set; }
        [Key(7)]
        public int Version { get; set; }
    }
}