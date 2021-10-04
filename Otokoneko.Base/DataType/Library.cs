using System.IO;
using System.Linq;
using MessagePack;
#if SERVER
#endif

namespace Otokoneko.DataType
{
    public enum FileStructType
    {
        None = 0,
        Image = 1,
        Chapter = 2,
        Series = 3,
        Manga = 4,
    }

    public enum LibraryType
    {
        None,
        Local,
        Ftp
    }

    [MessagePackObject]
    public partial class FileTreeNode
    {
        [Key(0)] public long ObjectId { get; set; }
        [Key(1)] public long ParentId { get; set; }
        [Key(2)] public string FullName { get; set; }
        [Key(3)] public bool IsDirectory { get; set; }
        [Key(4)] public FileStructType StructType { get; set; }
        [Key(5)] public string Authentication { get; set; }

        [IgnoreMember]
        public string Name => IsDirectory
            ? FullName.Split(Path.DirectorySeparatorChar).Last()
            : Path.GetFileNameWithoutExtension(FullName);
    }

    [MessagePackObject]
    public partial class FileTreeRoot
    {
        [Key(0)] public long ObjectId { get; set; }
        [Key(1)] public string Name { get; set; }
        [Key(2)] public string Host { get; set; }
        [Key(3)] public ushort Port { get; set; }
        [Key(4)] public string Path { get; set; }
        [Key(5)] public string Scheme { get; set; }
        [Key(6)] public string Username { get; set; }
        [Key(7)] public string Password { get; set; }
        [Key(8)] public string ScraperName { get; set; }
    }
}