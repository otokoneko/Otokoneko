using System.Collections.Generic;
using MessagePack;
using Otokoneko.DataType;

namespace Otokoneko.Client
{
    [MessagePackObject(true)]
    public class SearchOption
    {
        public bool Pageable { get; set; } = true;
        public int PageSize { get; set; } = 35;
        public OrderType OrderType { get; set; } = OrderType.UpdateTime;
        public bool Asc { get; set; } = false;
    }

    public enum WindowMode
    {
        NormalWindow,
        FullScreen,
        BorderlessWindow,
        BorderlessWindowWithControlButton,
    }

    public enum AutoCropMode
    {
        None,
        RightToLeft,
        LeftToRight,
    }

    [MessagePackObject(true)]
    public class WindowOption
    {
        public WindowMode WindowMode { get; set; } = WindowMode.BorderlessWindowWithControlButton;
        public bool ShowWindowControlButton { get; set; } = false;
    }

    public enum ImageDisplayMode
    {
        ImageListMode,
        SinglePageMode,
        DoublePageMode
    }

    public enum ScaleMode
    {
        Auto,
        FitWidth,
        FitHeight,
        Locked
    }

    [MessagePackObject(true)]
    public class ThemeOption
    {
        public bool DarkMode { get; set; } = true;
        public string Color { get; set; } = "Purple";
    }

    [MessagePackObject(true)]
    public class MangaReadOption
    {
        public WindowOption WindowOption { get; set; } = new WindowOption();
        public ImageDisplayMode ImageDisplayMode { get; set; } = ImageDisplayMode.ImageListMode;
        public AutoCropMode AutoCropMode { get; set; } = AutoCropMode.RightToLeft;
        public ScaleMode ScaleMode { get; set; } = ScaleMode.Locked;
        public double ScaleValue { get; set; } = 1;
    }

    [MessagePackObject(true)]
    public class TagDisplayOption
    {
        public Dictionary<long, string> TagTypeColorDictionary { get; set; } = new Dictionary<long, string>();
        public double FontSize { get; set; } = 14;
    }

    [MessagePackObject(true)]
    public class CacheOption
    {
        public long MaxFileCacheSize { get; set; } = 50 * 1024 * 1024;
    }

    [MessagePackObject(true)]
    public class Setting
    {
        public SearchOption SearchOption { get; set; } = new SearchOption();
        public MangaReadOption MangaReadOption { get; set; } = new MangaReadOption();
        public TagDisplayOption TagDisplayOption { get; set; } = new TagDisplayOption();
        public ThemeOption ThemeOption { get; set; } = new ThemeOption();
        public CacheOption CacheOption { get; set; } = new CacheOption();

        public Setting Copy()
        {
            var bytes = MessagePackSerializer.Serialize(this);
            return MessagePackSerializer.Deserialize<Setting>(bytes);
        }
    }
}