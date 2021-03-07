using System;
using MessagePack;
using Newtonsoft.Json;
using SqlSugar;

namespace Otokoneko.DataType
{
    public partial class Manga
    {
        [IgnoreMember]
        public long PathId { get; set; }
        [SugarColumn(IsIgnore = true), IgnoreMember]
        public Image Cover { get; set; }
        [SugarColumn(IsIgnore = true), IgnoreMember]
        public FileTreeNode Path { get; set; }
    }

    public partial class Chapter
    {
        [IgnoreMember]
        public long MangaId { get; set; }
        [IgnoreMember]
        public long PathId { get; set; }
        [SugarColumn(IsIgnore = true), JsonIgnore, IgnoreMember]
        public FileTreeNode Path { get; set; }
    }

    public partial class Image
    {
        [IgnoreMember]
        public long PathId { get; set; }
        [JsonIgnore, IgnoreMember]
        public long ChapterId { get; set; }
        [SugarColumn(IsIgnore = true), JsonIgnore, IgnoreMember]
        public FileTreeNode Path { get; set; }
    }

    public class MangaTagMapping
    {
        [SugarColumn(UniqueGroupNameList = new[] { nameof(MangaId), nameof(TagId) })]
        public long MangaId { get; set; }
        [SugarColumn(UniqueGroupNameList = new[] { nameof(MangaId), nameof(TagId) })]
        public long TagId { get; set; }
        [SugarColumn(IsIgnore = true)]
        public Manga Manga { get; set; }
        [SugarColumn(IsIgnore = true)]
        public Tag Tag { get; set; }
    }

    public class Favorite
    {
        [SugarColumn(UniqueGroupNameList = new[] { nameof(UserId), nameof(EntityId) })]
        public long UserId { get; set; }
        [SugarColumn(UniqueGroupNameList = new[] { nameof(UserId), nameof(EntityId) })]
        public long EntityId { get; set; }
        public EntityType EntityType { get; set; }
        [SugarColumn(IsIgnore = true)]
        public Manga Manga { get; set; }
        [SugarColumn(IsIgnore = true)]
        public Chapter Chapter { get; set; }
        [SugarColumn(IsIgnore = true)]
        public Image Image { get; set; }
        public DateTime CreateTime { get; set; }
    }

    public partial class Tag
    {
        [SugarColumn(IsIgnore = true), IgnoreMember]
        public TagType Type { get; set; }
    }
}
