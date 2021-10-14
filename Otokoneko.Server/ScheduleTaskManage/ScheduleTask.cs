using System;
using System.IO;
using MessagePack;
using Otokoneko.DataType;
using Otokoneko.Plugins.Interface;

namespace Otokoneko.Server.ScheduleTaskManage
{
    #region DownloadTask

    [MessagePackObject]
    public sealed class DownloadMangaScheduleTask : ScheduleTask
    {
        [Key(6)]
        public string LibraryPath { get; set; }

        [Key(7)]
        public string MangaUrl { get; set; }

        [Key(8)]
        public string MangaPath { get; set; }

        public override int Priority => int.MaxValue - 1;

        public DownloadMangaScheduleTask(string mangaUrl, string libraryPath, string name)
        {
            MangaUrl = mangaUrl;
            LibraryPath = libraryPath;
            Name = name;
        }

        protected override void OnUpdated()
        {
            if ((Status == TaskStatus.Success || Status == TaskStatus.Fail) && MangaPath != null && File.Exists(Path.Combine(MangaPath, ".ignore")))
            {
                File.Delete(Path.Combine(MangaPath, ".ignore"));
            }
            base.OnUpdated();
        }

        public override bool Equals(object obj)
        {
            return obj is DownloadMangaScheduleTask e && MangaUrl == e.MangaUrl && LibraryPath == e.LibraryPath;
        }

        public override int GetHashCode()
        {
            return MangaUrl.GetHashCode() * 10007 + LibraryPath.GetHashCode();
        }
    }

    public sealed class DownloadChapterScheduleTask : ScheduleTask
    {
        public string ChapterUrl { get; set; }
        public string ChapterPath { get; set; }

        public override int Priority => int.MaxValue - 2;

        public DownloadChapterScheduleTask(string chapterUrl, string chapterPath, string name, ScheduleTask parent)
        {
            ChapterPath = chapterPath;
            ChapterUrl = chapterUrl;
            Name = name;
            Parent = parent;
        }

        protected override void OnUpdated()
        {
            if (Status == TaskStatus.Success && ChapterPath != null && File.Exists(Path.Combine(ChapterPath, ".ignore")))
            {
                File.Delete(Path.Combine(ChapterPath, ".ignore"));
            }
            base.OnUpdated();
        }

        public override bool Equals(object obj)
        {
            return obj is DownloadChapterScheduleTask e && ChapterUrl == e.ChapterUrl && ChapterPath == e.ChapterPath;
        }

        public override int GetHashCode()
        {
            return ChapterUrl.GetHashCode() * 10007 + ChapterPath.GetHashCode();
        }
    }

    public sealed class DownloadImageScheduleTask : ScheduleTask
    {
        public string ImageUrl { get; set; }
        public string ImagePath { get; set; }
        public bool CouldCover { get; set; }

        public override int Priority => int.MaxValue - 3;

        public DownloadImageScheduleTask(string imageUrl, string imagePath, string name, ScheduleTask parent)
        {
            ImageUrl = imageUrl;
            ImagePath = imagePath;
            Name = name;
            Parent = parent;
        }

        public override bool Equals(object obj)
        {
            return obj is DownloadImageScheduleTask e && ImageUrl == e.ImageUrl && ImagePath == e.ImagePath;
        }

        public override int GetHashCode()
        {
            return ImageUrl.GetHashCode() * 10007 + ImagePath.GetHashCode();
        }
    }

    #endregion

    #region ScanTask

    [MessagePackObject]
    public sealed class ScanLibraryTask : ScheduleTask
    {
        [Key(6)]
        public long LibraryId { get; set; }

        public override int Priority => int.MaxValue - 4;

        public ScanLibraryTask(long libraryId, string name)
        {
            LibraryId = libraryId;
            Name = name;
        }

        protected override void OnUpdated()
        {
            if (Status == TaskStatus.Success || Status == TaskStatus.Fail)
            {
                Children.Clear();
                GC.Collect();
            }
            base.OnUpdated();
        }

        public override bool Equals(object obj)
        {
            return obj is ScanLibraryTask e && LibraryId == e.LibraryId;
        }

        public override int GetHashCode()
        {
            return (int)LibraryId;
        }
    }

    public sealed class ScanMangaTask : ScheduleTask
    {
        public override int Priority => int.MaxValue - 5;

        public FileTreeNode MangaPath { get; set; }

        public IMetadataScraper Scraper { get; set; }

        public ScanMangaTask(FileTreeNode mangaPath, string name, IMetadataScraper scraper, ScheduleTask parent)
        {
            MangaPath = mangaPath;
            Scraper = scraper;
            Name = name;
            Scraper = scraper;
            Parent = parent;
        }

        public override bool Equals(object obj)
        {
            return obj is ScanMangaTask e && MangaPath == e.MangaPath;
        }

        public override int GetHashCode()
        {
            return MangaPath.GetHashCode();
        }
    }

    #endregion
}