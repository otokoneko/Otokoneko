using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NaturalSort.Extension;
using Newtonsoft.Json;
using Otokoneko.DataType;
using Otokoneko.Plugins.Interface;
using Otokoneko.Server.MangaManage;

namespace Otokoneko.Server.Converter
{
    internal class MangaBuilder
    {
        private readonly Manga _manga;

        public Manga Manga => new Manga()
        {
            Title = _manga.Title,
            ObjectId = _manga.ObjectId,
            Aliases = _manga.Aliases,
            Chapters = _manga.Chapters,
            Cover = _manga.Cover,
            CoverId = _manga.CoverId,
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now,
            Description = _manga.Description,
            Path = _manga.Path,
            PathId = _manga.PathId,
            Tags = _manga.Tags,
            Version = _manga.Version
        };

        private MangaDetail _detail;
        private readonly Dictionary<string, List<Tuple<string, string>>> _orderDictionary;
        private readonly Dictionary<string, TagType> _tagTypes;

        public MangaBuilder(long objectId)
        {
            _manga = new Manga
            {
                ObjectId = objectId,
                Tags = new List<Tag>()
            };
            _orderDictionary = new Dictionary<string, List<Tuple<string, string>>>();
            _tagTypes = new Dictionary<string, TagType>();
        }

        private void AddTag(string tagTypeName, string tagName)
        {
            if (!_tagTypes.TryGetValue(tagTypeName, out var type))
            {
                type = new TagType()
                {
                    Name = tagTypeName
                };
                _tagTypes.Add(tagTypeName, type);
            }
            _manga.Tags.Add(new Tag()
            {
                Name = tagName,
                Type = type
            });
        }

        private void ReorderChapters()
        {
            for (var i = 0; i < _manga.Chapters.Count; i++)
            {
                var chapter = _manga.Chapters[i];
                chapter.ChapterClass ??= "未分类";
                chapter.Order = i;
            }

            foreach (var chapterClass in _orderDictionary.Keys)
            {
                var dict = new Dictionary<string, Tuple<int, string>>();
                var chapterMap = _orderDictionary[chapterClass];
                int chapterIndex;
                for (chapterIndex = 0; chapterIndex < chapterMap.Count; chapterIndex++)
                {
                    dict[chapterMap[chapterIndex].Item1] =
                        new Tuple<int, string>(chapterIndex * 10, chapterMap[chapterIndex].Item2);
                }
                foreach (var chapter in _manga.Chapters)
                {
                    var name = chapter.Path.FullName;
                    if (!dict.ContainsKey(name)) continue;
                    (chapter.Order, chapter.Title) = dict[name];
                    chapter.ChapterClass = chapterClass;
                }
            }

            _manga.Chapters = _manga.Chapters.OrderBy(it => it.Order).ToList();
        }

        public MangaDetail Detail
        {
            set
            {
                _detail ??= value ?? throw new ArgumentNullException();
                _manga.Title = _detail.Name;
                _manga.Description = _detail.Description;
                _manga.Aliases = _detail.Aliases != null ? string.Join('\n', _detail.Aliases) : null;

                if (_detail.Tags != null)
                {
                    foreach (var tag in _detail.Tags)
                    {
                        AddTag(tag.Type, tag.Name);
                    }
                }

                if (_detail.Chapters == null) return;

                var orderDictionary = _detail
                    .Chapters
                    .GroupBy(chapter => chapter.Type)
                    .ToDictionary(
                        it => it.Key ?? "未分类",
                        it => it.Select(detail => new Tuple<string, string>(detail.Id, detail.Name)).ToList());

                foreach (var (k, v) in orderDictionary)
                {
                    _orderDictionary[k] = v;
                }

                if (_manga.Chapters != null) ReorderChapters();
            }
        }

        public string Description
        {
            set => _manga.Description ??= value ?? throw new ArgumentNullException();
        }

        public FileTreeNode Path
        {
            set
            {
                _manga.Path ??= value ?? throw new ArgumentNullException();
                _manga.PathId = value.ObjectId;
            }
        }

        public Image Cover
        {
            set
            {
                _manga.Cover ??= value ?? throw new ArgumentNullException();
                _manga.CoverId = value.ObjectId;
                _manga.Cover.ChapterId = _manga.ObjectId;
            }
        }

        public string Title
        {
            set => _manga.Title = value ?? throw new ArgumentNullException();
        }

        public List<Chapter> Chapters
        {
            set
            {
                _manga.Chapters = value ?? throw new ArgumentNullException();
                foreach (var chapter in value)
                {
                    chapter.MangaId = _manga.ObjectId;
                }

                if (_orderDictionary != null) ReorderChapters();
            }
        }

        public int Version
        {
            set => _manga.Version = value;
        }
    }

    public class FileTreeNodeToMangaConverter
    {
        public MangaManager MangaManager { get; set; }

        private List<FileTreeNode> GetChapterPaths(FileTreeNode path)
        {
            var results = new List<FileTreeNode>();
            switch (path.StructType)
            {
                case FileStructType.Chapter:
                    results.Add(path);
                    break;
                case FileStructType.None:
                case FileStructType.Series:
                case FileStructType.Manga:
                    if (path.Children != null)
                    {
                        foreach (var child in path.Children)
                        {
                            results.AddRange(GetChapterPaths(child));
                        }
                    }
                    break;
            }

            if (path.StructType == FileStructType.Manga && results.Count == 0)
            {
                results.Add(path);
            }
            return results;
        }

        private string GetClassName(FileTreeNode path)
        {
            var parent = path;
            while (parent.StructType != FileStructType.Manga)
            {
                if (parent.StructType == FileStructType.Series)
                {
                    return parent.Name;
                }

                parent = parent.Parent;
            }

            return null;
        }

        public async ValueTask<Manga> CreateManga(FileTreeNode path)
        {
            var mangaBuilder = new MangaBuilder(-1)
            {
                Title = path.Name,
                Path = path,
                Description = string.Empty,
            };
            foreach (var child in path.Children)
            {
                switch (child.Name)
                {
                    case "cover":
                        if (!FileTreeNodeFormatter.IsImageFile(child)) continue;
                        mangaBuilder.Cover = CreateImage(child);
                        continue;
                    case "detail":
                        if (child.IsDirectory) continue;
                        mangaBuilder.Detail =
                            JsonConvert.DeserializeObject<MangaDetail>(Encoding.UTF8.GetString(await child.ReadAllBytes()));
                        continue;
                }
            }

            var chapters = GetChapterPaths(path).Select(CreateChapter).Where(chapter => chapter != null).ToList();

            mangaBuilder.Chapters =
                chapters.OrderBy(p => p.Title, StringComparison.OrdinalIgnoreCase.WithNaturalSort())
                    .ToList();

            mangaBuilder.Version = 0;
            return mangaBuilder.Manga;
        }

        public async ValueTask<Tuple<Manga, List<Chapter>>> UpdateManga(FileTreeNode path)
        {
            var oldManga = await MangaManager.GetManga(path.ObjectId);
            oldManga.Cover = await MangaManager.GetImage(oldManga.CoverId);
            var mangaBuilder = new MangaBuilder(oldManga.ObjectId)
            {
                Title = oldManga.Title,
                Path = path,
                Description = oldManga.Description,
                Cover = oldManga.Cover
            };
            var oldChapters = new List<Chapter>(oldManga.Chapters);
            foreach (var child in path.Children)
            {
                if (child.Name != "detail") continue;
                if (child.IsDirectory) break;
                mangaBuilder.Detail =
                    JsonConvert.DeserializeObject<MangaDetail>(Encoding.UTF8.GetString(await child.ReadAllBytes()));
            }

            var chapters = GetChapterPaths(path).Select(CreateChapter).Where(chapter => chapter != null).ToList();
            var newChapters = new List<Chapter>();

            for (int i = 0; i < chapters.Count; i++)
            {
                var chapter = chapters[i];
                var oldChapter = oldChapters.Find(it => it.PathId == chapter.PathId);
                if (oldChapter != null)
                {
                    oldChapter.Path = chapter.Path;
                    oldChapter.ChapterClass = chapter.ChapterClass;
                    chapters[i] = oldChapter;
                }
                else
                {
                    newChapters.Add(chapter);
                }
            }

            mangaBuilder.Chapters =
                chapters.OrderBy(p => p.Title, StringComparison.OrdinalIgnoreCase.WithNaturalSort())
                    .ToList();

            mangaBuilder.Version = oldManga.Version + 1;
            return new Tuple<Manga, List<Chapter>>(mangaBuilder.Manga, newChapters);
        }

        public async ValueTask<Manga> UpdateManga(Manga manga, MangaDetail detail)
        {
            var mangaBuilder = new MangaBuilder(manga.ObjectId)
            {
                Title = manga.Title,
                Path = manga.Path,
                Description = manga.Description,
                Cover = manga.Cover,
                Chapters = manga.Chapters,
                Detail = detail,
            };
            return mangaBuilder.Manga;
        }

        public Chapter CreateChapter(FileTreeNode path)
        {
            var chapter = new Chapter
            {
                Path = path,
                Title = path.Name,
                PathId = path.ObjectId,
                Images = new List<Image>()
            };
            chapter.UpdateTime = chapter.CreateTime = DateTime.Now;
            chapter.ChapterClass = GetClassName(path);
            var images = chapter.Images;
            path.Children = new List<FileTreeNode>(path
                .Children
                .OrderBy(p => p.Name, StringComparison.OrdinalIgnoreCase.WithNaturalSort()));
            images.AddRange(path.Children.Where(it => it.StructType == FileStructType.Image)
                .Select(item =>
                {
                    var image = CreateImage(item);
                    image.ChapterId = chapter.ObjectId;
                    image.Order = images.Count * 10;
                    return image;
                }));

            return chapter.Images.Count == 0 ? null : chapter;
        }

        public Image CreateImage(FileTreeNode path)
        {
            var image = new Image
            {
                PathId = path.ObjectId,
                Path = path,
                Height = 0,
                Width = 0
            };
            return image;
        }

    }
}
