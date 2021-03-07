using System;
using MessagePack;
#if SERVER
using SqlSugar;
#endif

namespace Otokoneko.DataType
{
    public enum SessionKeepTime
    {
        OneDay,
        OneWeek,
        OneMonth,
        OneYear
    };

    public enum RegisterResult
    {
        Unknown,
        Success,
        InvitationCodeNotFound,
        InvitationCodeHasBeenUsed,
        UsernameRepeated
    }

    public enum UserAuthority
    {
        Root = 0,
        Admin = 1,
        User = 100,
        Banned = int.MaxValue - 1,
        Visitor = int.MaxValue
    }

    [MessagePackObject]
    public class User
    {
#if SERVER
        [SugarColumn(IsPrimaryKey = true)]
#endif
        [Key(0)]
        public long Id { get; set; }
#if SERVER
        [SugarColumn(UniqueGroupNameList = new[] { nameof(Name) })]
#endif
        [Key(1)]
        public string Name { get; set; }
        [IgnoreMember]
        public byte[] Salt { get; set; }
        [IgnoreMember]
        public string Password { get; set; }
        [Key(2)]
        public UserAuthority Authority { get; set; }
    }

    [MessagePackObject]
    public class Invitation
    {
#if SERVER
        [SugarColumn(IsPrimaryKey = true)]
#endif
        [Key(0)]
        public long ObjectId { get; set; }
        [IgnoreMember]
        public long SenderId { get; set; }
#if SERVER
        [SugarColumn(IsIgnore = true)]
#endif
        [Key(1)]
        public User Sender { get; set; }
        [Key(2)]
        public UserAuthority Authority { get; set; }
        [Key(3)]
        public string InvitationCode { get; set; }
        [IgnoreMember]
        public long ReceiverId { get; set; }
#if SERVER
        [SugarColumn(IsIgnore = true)]
#endif
        [Key(4)]
        public User Receiver { get; set; }
        [Key(5)]
        public DateTime CreateTime { get; set; }
        [Key(6)]
        public DateTime UsedTime { get; set; }
    }

    [MessagePackObject]
    public class UserHelper
    {
        [Key(0)]
        public string Name { get; set; }
        [Key(1)]
        public string Password { get; set; }
        [Key(2)]
        public SessionKeepTime SessionKeepTime { get; set; }
        [Key(3)]
        public string InvitationCode { get; set; }
    }
}