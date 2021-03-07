using MessagePack;

namespace Otokoneko.DataType
{
    public enum OrderType
    {
        Default,
        CreateTime,
        UpdateTime
    }

    public enum QueryType
    {
        Keyword,
        Favorite,
        History
    }


    [MessagePackObject]
    public class MangaQueryHelper
    {
        [Key(0)]
        public string QueryString { get; set; }
        [Key(1)]
        public int Offset { get; set; }
        [Key(2)]
        public int Limit { get; set; }
        [Key(3)]
        public QueryType QueryType { get; set; }
        [Key(4)]
        public OrderType OrderType { get; set; }
        [Key(5)]
        public bool Asc { get; set; }
    }

    [MessagePackObject]
    public class TagQueryHelper
    {
        [Key(0)]
        public string QueryString { get; set; }
        [Key(1)]
        public int Offset { get; set; }
        [Key(2)]
        public int Limit { get; set; }
        [Key(3)]
        public long TypeId { get; set; }
    }
}