using System;
using System.Collections.Generic;
using MessagePack;
#if SERVER
using SqlSugar;
#endif

namespace Otokoneko.DataType
{
    public enum ServerNotify
    {
        Unknown,
        NewMessageSent
    }

    [MessagePackObject]
    public class Message
    {
#if SERVER
        [SugarColumn(IsPrimaryKey = true)]
#endif
        [Key(0)]
        public long ObjectId { get; set; }
#if SERVER
        [SugarColumn(IsOnlyIgnoreUpdate = true)]
#endif
        [Key(1)]
        public DateTime CreateUtcTime { get; set; }
        [Key(2)]
        public long SenderId { get; set; }
#if SERVER
        [SugarColumn(IsNullable = true)]
#endif
        [Key(3)]
        public string Data { get; set; }
#if SERVER
        [SugarColumn(IsIgnore = true)]
#endif
        [IgnoreMember]
        public List<long> Receivers { get; set; }
        [Key(4)]
        public bool Checked { get; set; }
    }

    [MessagePackObject]
    public class MessageUserMapping
    {
#if SERVER
        [SugarColumn(UniqueGroupNameList = new[] { nameof(MessageId), nameof(ReceiverId) })]
#endif
        [Key(0)]
        public long MessageId { get; set; }
#if SERVER
        [SugarColumn(UniqueGroupNameList = new[] { nameof(MessageId), nameof(ReceiverId) })]
#endif
        [Key(1)]
        public long ReceiverId { get; set; }
        [Key(2)]
        public bool Checked { get; set; }
    }
}
