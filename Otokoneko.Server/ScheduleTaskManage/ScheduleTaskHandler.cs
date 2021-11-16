using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Newtonsoft.Json;
using Otokoneko.DataType;
using Otokoneko.Plugins.Interface;
using Otokoneko.Server.Converter;
using Otokoneko.Server.MangaManage;
using Otokoneko.Server.MessageBox;
using Otokoneko.Server.PluginManage;
using Otokoneko.Server.Utils;
using TaskStatus = Otokoneko.DataType.TaskStatus;

namespace Otokoneko.Server.ScheduleTaskManage
{
    public interface ITaskHandler<in T>
    where T:ScheduleTask
    {
        public ValueTask<TaskStatus> Execute(T task);
    }

    public class DownloadMangaTaskHandler : ITaskHandler<DownloadMangaScheduleTask>
    {
        public MessageManager MessageManager { get; set; }
        public PluginManager PluginManager { get; set; }
        public ILog Logger { get; set; }

        private async ValueTask SendExceptionMessage(string taskName, string exception)
        {
            var message = new Message()
            {
                Data = string.Format(MessageTemplate.DownloadExceptionMessage, taskName, exception)
            };
            await MessageManager.Send(message, new HashSet<UserAuthority>() {UserAuthority.Admin, UserAuthority.Root});
        }

        public async ValueTask<TaskStatus> Execute(DownloadMangaScheduleTask downloadMangaTask)
        {
            var downloader = PluginManager.MangaDownloaders.FirstOrDefault(it => it.IsLegalUrl(downloadMangaTask.MangaUrl, DownloadTaskType.Manga));
            if (downloader == null)
            {
                await SendExceptionMessage(downloadMangaTask.Name, "不存在匹配该链接的下载器");
                return TaskStatus.Fail;
            }

            if (!Directory.Exists(downloadMangaTask.LibraryPath))
            {
                await SendExceptionMessage(downloadMangaTask.Name, $"下载路径{downloadMangaTask.LibraryPath}不存在");
                return TaskStatus.Fail;
            }
            try
            {
                downloadMangaTask.Update(TaskStatus.Executing);
                var mangaDetail = await downloader.GetManga(downloadMangaTask.MangaUrl);
                downloadMangaTask.Name = mangaDetail.Name;
                downloadMangaTask.MangaPath = Path.Combine(downloadMangaTask.LibraryPath, downloader.Name, mangaDetail.Id);
                Directory.CreateDirectory(downloadMangaTask.MangaPath);
                await File.WriteAllTextAsync(Path.Combine(downloadMangaTask.MangaPath, "detail.json"), JsonConvert.SerializeObject(mangaDetail));
                File.Create(Path.Combine(downloadMangaTask.MangaPath, ".ignore")).Close();
                if (mangaDetail.Cover != null)
                {
                    var imageUrl = mangaDetail.Cover;
                    var imagePath = Path.Combine(downloadMangaTask.MangaPath, "cover");
                    var imageTask = new DownloadImageScheduleTask(imageUrl, imagePath, "cover", downloadMangaTask)
                    {
                        CouldCover = true
                    };
                    downloadMangaTask.Children.Add(imageTask);
                }

                foreach (var chapterDetail in mangaDetail.Chapters)
                {
                    var chapterTask = new DownloadChapterScheduleTask(
                        chapterDetail.Url,
                        Path.Combine(downloadMangaTask.MangaPath, chapterDetail.Id),
                        chapterDetail.Name,
                        downloadMangaTask);
                    downloadMangaTask.Children.Add(chapterTask);
                }

                downloadMangaTask.Counter = new AtomicCounter(0, downloadMangaTask.Children.Count);
                return TaskStatus.Running;
            }
            catch (Exception e)
            {
                Logger.Warn($"Download {downloadMangaTask.MangaUrl} fail", e);
                await SendExceptionMessage(downloadMangaTask.Name, e.Message);
                return TaskStatus.Fail;
            }
        }
    }

    public class DownloadChapterTaskHandler : ITaskHandler<DownloadChapterScheduleTask>
    {
        public PluginManager PluginManager { get; set; }
        public ILog Logger { get; set; }

        public async ValueTask<TaskStatus> Execute(DownloadChapterScheduleTask downloadChapterTask)
        {
            var downloader = PluginManager.MangaDownloaders.FirstOrDefault(it => it.IsLegalUrl(downloadChapterTask.ChapterUrl, DownloadTaskType.Chapter));
            if (downloader == null)
            {
                return TaskStatus.Fail;
            }
            try
            {
                downloadChapterTask.Update(TaskStatus.Executing);
                if (Directory.Exists(downloadChapterTask.ChapterPath) && 
                    !File.Exists(Path.Combine(downloadChapterTask.ChapterPath, ".ignore")))
                {
                    return TaskStatus.Success;
                }
                Directory.CreateDirectory(downloadChapterTask.ChapterPath);
                File.Create(Path.Combine(downloadChapterTask.ChapterPath, ".ignore")).Close();
                var imageList = await downloader.GetChapter(downloadChapterTask.ChapterUrl);
                for (var j = 0; j < imageList.Count; j++)
                {
                    var imageUrl = imageList[j];
                    var imagePath = Path.Combine(downloadChapterTask.ChapterPath, j.ToString());
                    var imageTask = new DownloadImageScheduleTask(imageUrl, imagePath, j.ToString(), downloadChapterTask);
                    downloadChapterTask.Children.Add(imageTask);
                }

                downloadChapterTask.Counter = new AtomicCounter(0, downloadChapterTask.Children.Count);
                return TaskStatus.Running;
            }
            catch (Exception e)
            {
                Logger.Warn($"Download {downloadChapterTask.ChapterUrl} fail", e);
                return TaskStatus.Fail;
            }
        }
    }

    public class DownloadImageTaskHandler : ITaskHandler<DownloadImageScheduleTask>
    {
        public PluginManager PluginManager { get; set; }
        public ILog Logger { get; set; }

        private long GetFileSize(string filename)
        {
            var info = new FileInfo(filename);
            return info.Length;
        }

        public async ValueTask<TaskStatus> Execute(DownloadImageScheduleTask downloadImageTask)
        {
            var downloader = PluginManager.MangaDownloaders.FirstOrDefault(it => it.IsLegalUrl(downloadImageTask.ImageUrl, DownloadTaskType.Image));
            var samePool = ArrayPool<byte>.Shared;
            byte[] buffer = null;
            MemoryStream stream = null;
            try
            {
                if (downloader == null)
                {
                    throw new ArgumentException($"未找到合适的下载器，url: {downloadImageTask.ImageUrl}");
                }
                downloadImageTask.Update(TaskStatus.Executing);
                var parent = Path.GetDirectoryName(downloadImageTask.ImagePath);
                var fileName = Path.GetFileName(downloadImageTask.ImagePath);
                var existsFile = Directory.GetFiles(parent, fileName + ".*").FirstOrDefault();
                if (!downloadImageTask.CouldCover && existsFile != null)
                {
                    return TaskStatus.Success;
                }

                using var content = await downloader.GetImage(downloadImageTask.ImageUrl);
                long length = content.Headers.ContentLength ?? 0;
                
                if (length > 0)
                {
                    buffer = samePool.Rent((int)length);
                    stream = new MemoryStream(buffer);
                }
                else
                {
                    stream = new MemoryStream(128 * 1024);
                }

                using var source = new CancellationTokenSource();
                var token = source.Token;
                var timeout = TimeSpan.FromMinutes(5);
                var copyToAsync = content.CopyToAsync(stream, token);
                if (await Task.WhenAny(copyToAsync, Task.Delay(timeout, token)) != copyToAsync || stream.Position < length)
                {
                    throw new TimeoutException();
                }
                length = stream.Position;

                if (existsFile != null && GetFileSize(existsFile) == length)
                {
                    return TaskStatus.Success;
                }

                ImageUtils.ReleaseMemory(TimeSpan.FromMinutes(10));
                if (!ImageUtils.ImageCheck(stream, length, out var format))
                {
                    throw new BadImageFormatException();
                }
                await using var fs = File.OpenWrite(existsFile ?? $"{downloadImageTask.ImagePath}.{format.FileExtensions.First()}");
                fs.SetLength(0);
                await fs.WriteAsync((buffer ?? stream.GetBuffer()).AsMemory(0, (int)length));
                return TaskStatus.Success;
            }
            catch (Exception e)
            {
                Logger.Warn($"Download {downloadImageTask.ImageUrl} fail", e);
                return TaskStatus.Fail;
            }
            finally
            {
                stream?.Close();
                if (buffer != null) samePool.Return(buffer);
            }
        }
    }

    public class ScanLibraryTaskHandler : ITaskHandler<ScanLibraryTask>
    {
        public MessageManager MessageManager { get; set; }
        public LibraryManager LibraryManager { get; set; }
        public PluginManager PluginManager { get; set; }
        public ILog Logger { get; set; }

        private async ValueTask SendExceptionMessage(string taskName, string exception)
        {
            var message = new Message()
            {
                Data = string.Format(MessageTemplate.LibraryScanExceptionMessage, taskName, exception)
            };
            await MessageManager.Send(message, new HashSet<UserAuthority>() { UserAuthority.Admin, UserAuthority.Root });
        }

        private async ValueTask SendSuccessMessage(string taskName, int createNumber, int updateNumber)
        {
            var message = new Message()
            {
                Data = string.Format(MessageTemplate.LibraryScanResultMessage, taskName, createNumber, updateNumber)
            };
            await MessageManager.Send(message, new HashSet<UserAuthority>() {UserAuthority.Admin, UserAuthority.Root});
        }

        public async ValueTask<TaskStatus> Execute(ScanLibraryTask scanLibraryTask)
        {
            try
            {
                scanLibraryTask.Update(TaskStatus.Executing);
                var library = LibraryManager.GetLibrary(scanLibraryTask.LibraryId);
                if (library == null)
                {
                    await SendExceptionMessage(scanLibraryTask.Name, "该库不存在");
                    return TaskStatus.Fail;
                }
                var mangaPaths = LibraryManager.CheckUpdates(scanLibraryTask.LibraryId);
                var scraper = PluginManager.MetadataScrapers.FirstOrDefault(it => it.Name == library.ScraperName);
                foreach (var mangaPath in mangaPaths)
                {
                    scanLibraryTask.Children.Add(new ScanMangaTask(mangaPath, mangaPath.Name, scraper, scanLibraryTask));
                }

                scanLibraryTask.Counter = new AtomicCounter(0, scanLibraryTask.Children.Count);
                await SendSuccessMessage(scanLibraryTask.Name, mangaPaths.Count(it => it.IsNewItem),
                    mangaPaths.Count(it => !it.IsNewItem));
                return TaskStatus.Running;
            }
            catch (Exception e)
            {
                Logger.Warn($"Scan library {scanLibraryTask.Name} fail", e);
                await SendExceptionMessage(scanLibraryTask.Name, e.Message);
                return TaskStatus.Fail;
            }
        }
    }

    public class ScanMangaTaskHandler: ITaskHandler<ScanMangaTask>
    {
        public ILog Logger { get; set; }
        public MangaManager MangaManager { get; set; }
        public LibraryManager LibraryManager { get; set; }
        public MessageManager MessageManager { get; set; }
        public FileTreeNodeToMangaConverter FileTreeNodeToMangaConverter { get; set; }

        public async ValueTask<TaskStatus> Execute(ScanMangaTask scanMangaTask)
        {
            try
            {
                if (scanMangaTask.MangaPath.IsNewItem)
                {
                    scanMangaTask.Update(TaskStatus.Executing);
                    var manga = await FileTreeNodeToMangaConverter.CreateManga(scanMangaTask.MangaPath);
                    
                    FileTreeNode coverPath = null;
                    
                    if (manga.Cover == null)
                    {
                        coverPath = LibraryManager.GenerateThumbnail(manga.Chapters.First().Images.First().Path);
                        ImageUtils.ReleaseMemory(TimeSpan.FromMinutes(10));
                        manga.Cover = FileTreeNodeToMangaConverter.CreateImage(coverPath);
                        manga.CoverId = manga.Cover.ObjectId;
                    }

                    if (scanMangaTask.Scraper != null)
                    {
                        var context = new MangaDetail() {Name = manga.Title};
                        await scanMangaTask.Scraper.ScrapeMetadata(context);
                        manga = await FileTreeNodeToMangaConverter.UpdateManga(manga, context);
                    }

                    manga.Tags = await MangaManager.Insert(manga.Tags);
                    var success = await MangaManager.Insert(manga);
                    if (!success)
                    {
                        Logger.Info($"Create {manga.Title} fail");
                        if (coverPath != null)
                        {
                            coverPath.Delete();
                        }
                        return TaskStatus.Fail;
                    }
                    else
                    {
                        LibraryManager.StoreFileTree(scanMangaTask.MangaPath);
                        if (coverPath != null) LibraryManager.StoreFileTree(coverPath);
                        return TaskStatus.Success;
                    }
                }
                else
                {
                    var (manga, chapters) = await FileTreeNodeToMangaConverter.UpdateManga(scanMangaTask.MangaPath);
                    manga.Tags = await MangaManager.Insert(manga.Tags);
                    var success = await MangaManager.Update(manga, true);
                    if (!success)
                    {
                        Logger.Info($"Update {manga.Title} fail");
                        return TaskStatus.Fail;
                    }
                    else
                    {
                        LibraryManager.StoreFileTree(scanMangaTask.MangaPath);

                        var subscribers = await MangaManager.GetSubscribers(manga.ObjectId);
                        var message = new Message()
                        {
                            Data = string.Format(MessageTemplate.MangaUpdateMessage, 
                                manga.Title,
                                string.Join(", ", chapters.Select(it => it.Title)))
                        };
                        await MessageManager.Send(message, subscribers.ToHashSet());
                        return TaskStatus.Success;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Warn($"Scan manga {scanMangaTask.Name} fail", e);
                return TaskStatus.Fail;
            }
        }
    }
}