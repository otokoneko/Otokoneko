using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Win32;
using Otokoneko.Client.WPFClient.View;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    class MangaDetailState :INavigationState
    {
        public MangaDetailViewModel ViewModel { get; set; }

        public INavigationViewModel GetViewModel()
        {
            return ViewModel;
        }
    }

    public static partial class Constant
    {
        public static Color IsFavoriteColor = Colors.Red;
        public static Color IsNotFavoriteColor = Color.FromRgb(0xA5, 0xA5, 0xA5);

        public const string StartReadingNotice = "开始阅读";
        public const string ContinueReadingNotice = "继续阅读";
        public const string NoReadHistoryNotice = "从未读过这本书";

        public const string CreateTimeTemplate = "创建时间：{0}";
        public const string UpdateTimeTemplate = "更新时间：{0}";

        public const string HasScoredTemplate = "当前评分：{0}";
        public const string HasNotScoredTemplate = "尚未评分";

        public const string ReadHistoryTemplate = "上次读到：\n{0}\n{1}";

        public const string DeletedMangaNotice = "确定删除漫画 {0}？";

        public const string MangaNotFound = "该漫画不存在";
    }

    class MangaDetailViewModel:BaseViewModel, INavigationViewModel
    {
        public NavigationService NavigationService { get; set; }

        public Manga Manga { get; set; }

        private bool Loaded { get; set; }

        public ObservableCollection<Tuple<string, ObservableCollection<Chapter>>> ChapterClasses { get; set; }

        public int SelectedChapterClassIndex { get; set; } = 0;

        public BitmapImage Cover { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string CreateTime { get; set; }
        public string UpdateTime { get; set; }
        public string RecentReadTime { get; set; }
        public ObservableCollection<DisplayTag> Tags { get; set; }
        public string Aliases { get; set; }
        public Color IsFavoriteColor { get; set; }
        public string StartReadingString { get; set; }
        public int Score { get; set; }
        public string ScoreText { get; set; }
        public bool Editable { get; set; } = false;
        private ReadProgress _lastReadProgress;

        public ICommand EditMangaCommand => new AsyncCommand(async () =>
        {
            Editable = true;
            OnPropertyChanged(nameof(Editable));
            DisplayTags();
        });

        public ICommand StartReadingCommand => new AsyncCommand(async () =>
        {
            if(_lastReadProgress != null)
            {
                foreach (var (_, chapters) in ChapterClasses)
                {
                    var index = chapters.IndexOf(_lastReadProgress.Chapter);
                    if (index == -1) continue;
                    Read(chapters.ToList(), index);
                    return;
                }
            }

            foreach (var (_, chapters) in ChapterClasses)
            {
                var chapter = chapters.FirstOrDefault();
                if (chapter == null) continue;
                Read(chapters.ToList(), chapters.IndexOf(chapter));
                return;
            }
        });

        public ICommand ReadCommand => new AsyncCommand<Chapter>(async (chapter) =>
        {
            var chapters = ChapterClasses[SelectedChapterClassIndex].Item2.ToList();
            Read(chapters, chapters.IndexOf(chapter));
        });

        public ICommand ChangeMangaTags => new AsyncCommand(async () =>
        {
            var tagSelectionWindow = new TagSelectionWindow(-1, Manga.Tags);
            var result = tagSelectionWindow.ShowDialog();
            if (result != true) return;
            Manga.Tags = tagSelectionWindow.SelectedTags;
            DisplayTags();
        });

        public ICommand CancelEditCommand => new AsyncCommand(async () =>
        {
            Editable = false;
            OnPropertyChanged(nameof(Editable));
            await LoadManga();
        });

        public ICommand ConfirmEditCommand => new AsyncCommand(async () =>
        {
            Editable = false;
            OnPropertyChanged(nameof(Editable));
            Manga.Title = Title;
            Manga.Description = Description;
            Manga.Aliases = Aliases;
            Manga.Chapters = new List<Chapter>();
            var i = 0;
            foreach (var (chapterClass, chapters) in ChapterClasses)
            {
                foreach (var chapter in chapters)
                {
                    chapter.Order = i++;
                    chapter.ChapterClass = chapterClass;
                    Manga.Chapters.Add(chapter);
                }
            }

            Manga.Version++;
            var success = await Model.UpdateManga(Manga);
            MessageBox.Show(success ? Constant.UpdateSuccess : Constant.UpdateFail);
        });

        public ICommand SetFavoriteMangaCommand => new AsyncCommand(async () =>
        {
            Manga.IsFavorite = !Manga.IsFavorite;
            if (Manga.IsFavorite)
            {
                await Model.AddMangaFavorite(Manga.ObjectId);
            }
            else
            {
                await Model.RemoveMangaFavorite(Manga.ObjectId);
            }
            DisplayMetadata();
        });

        public ICommand DeleteMangaCommand => new AsyncCommand(async () =>
        {
            var result = MessageBox.Show(string.Format(Constant.DeletedMangaNotice, Manga.Title), Constant.OperateNotice, MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;
            var success = await Model.DeleteManga(Manga.ObjectId);
            if (success)
            {
                MessageBox.Show(Constant.DeleteSuccess);
                NavigationService.Navigate(new MangaSearchResultViewModel(NavigationService, null, QueryType.Keyword), false);
            }
            else
            {
                MessageBox.Show(Constant.DeleteFail);
            }
        });

        public ICommand CommentCommand => new AsyncCommand(async () =>
        {
            var comment = Manga.Comment ?? new Comment
            {
                EntityId = Manga.ObjectId,
                EntityType = EntityType.Manga,
                Score = 0
            };
            var commentForm = new CommentWindow(comment);
            var result = commentForm.ShowDialog();
            if (result != true) return;
            Manga.Comment = commentForm.Comment;
            comment.Version++;
            await Model.AddComment(Manga.Comment);
            DisplayScore();
        });

        public ICommand DownloadMangaCommand => new AsyncCommand(async () =>
        {
            var dialog = new SaveFileDialog();
            dialog.DefaultExt = ".zip";
            dialog.FileName = $"{Manga.Title}.zip";
            dialog.Filter = "Compress file (.zip)|*.zip";
            var result = dialog.ShowDialog();
            if (result != true)
            {
                return;
            }

            string filename = dialog.FileName;
            var stream = File.OpenWrite(filename);
            stream.SetLength(0);
            await Model.DownloadManga(Manga.ObjectId, stream);
        });

        public INavigationState GetState()
        {
            return new MangaDetailState()
            {
                ViewModel = this
            };
        }

        public MangaDetailViewModel(NavigationService navigationService, Manga manga)
        {
            NavigationService = navigationService;
            Manga = manga;
        }

        public async ValueTask LoadManga()
        {
            var manga = await Model.GetManga(Manga.ObjectId);
            if (manga == null)
            {
                MessageBox.Show(Constant.MangaNotFound);
                return;
            }
            Manga = manga;
            await Model.ListTagTypes();
            DisplayMetadata();
            DisplayScore();
            DisplayTags();
            DisplayReadHistory();
            DisplayChapters();
            var imageContent = await Model.GetImage(Manga.CoverId);
            Cover = Utils.ImageUtils.Convert(imageContent);
            OnPropertyChanged(nameof(Cover));
        }

        public async ValueTask OnLoaded()
        {
            if(Loaded) return;
            Loaded = true;
            await LoadManga();
        }

        private void Read(List<Chapter> chapters, int index)
        {
            if (index < 0 || index >= chapters.Count) return;
            var mangaReader = new MangaReader(chapters, index);
            mangaReader.Show();
        }

        #region DisplayData

        private void DisplayMetadata()
        {
            Title = Manga.Title;
            Description = Manga.Description;
            CreateTime = string.Format(Constant.CreateTimeTemplate,
                Utils.FormatUtils.FormatLocalDateTime(Manga.CreateTime));
            UpdateTime = string.Format(Constant.UpdateTimeTemplate,
                Utils.FormatUtils.FormatLocalDateTime(Manga.UpdateTime));
            IsFavoriteColor = Manga.IsFavorite ? Constant.IsFavoriteColor : Constant.IsNotFavoriteColor;
            Aliases = Manga.Aliases;
            OnPropertyChanged(nameof(Aliases));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(CreateTime));
            OnPropertyChanged(nameof(UpdateTime));
            OnPropertyChanged(nameof(IsFavoriteColor));
        }

        private void DisplayScore()
        {
            Score = Manga.Comment?.Score ?? 0;
            ScoreText = Manga.Comment == null
                ? Constant.HasNotScoredTemplate
                : string.Format(Constant.HasScoredTemplate, Manga.Comment.Score);
            OnPropertyChanged(nameof(Score));
            OnPropertyChanged(nameof(ScoreText));
        }

        private void DisplayTags()
        {
            Manga.Tags = Manga.Tags.OrderBy(it => it.TypeId).ToList();
            Tags = new ObservableCollection<DisplayTag>();
            foreach (var tag in Manga.Tags)
            {
                Tags.Add(new DisplayTag(tag)
                {
                    ClickCommand = Editable
                        ? ChangeMangaTags
                        : new AsyncCommand(async () =>
                        {
                            NavigationService.Navigate(
                                new MangaSearchResultViewModel(
                                    NavigationService,
                                    $"${tag.Name}$",
                                    QueryType.Keyword));
                        })
                });
            }

            if (Editable)
            {
                Tags.Add(new DisplayTag()
                {
                    ClickCommand = ChangeMangaTags,
                    Name = "+",
                    Color = Color.FromRgb(0x50, 0x50, 0x50)
                });
            }
            OnPropertyChanged(nameof(Tags));
        }

        private void DisplayReadHistory()
        {
            if (Manga.ReadProgresses != null && Manga.ReadProgresses.Count != 0)
            {
                _lastReadProgress = Manga.ReadProgresses.OrderBy(it => it.ReadTime).Last();
                _lastReadProgress.Chapter = Manga.Chapters.Single(chapter => chapter.ObjectId == _lastReadProgress.ChapterId);
                RecentReadTime = string.Format(Constant.ReadHistoryTemplate, 
                    _lastReadProgress.Chapter.Title,
                    _lastReadProgress.ReadTime);
                StartReadingString = Constant.ContinueReadingNotice;
            }
            else
            {
                RecentReadTime = Constant.NoReadHistoryNotice;
                StartReadingString = Constant.StartReadingNotice;
            }
            OnPropertyChanged(nameof(StartReadingString));
            OnPropertyChanged(nameof(RecentReadTime));
        }

        private void DisplayChapters()
        {
            var chapterClasses = Manga.Chapters
                .GroupBy(it => it.ChapterClass)
                .ToDictionary(it => it.Key, chapters => chapters);
            ChapterClasses =
                new ObservableCollection<Tuple<string, ObservableCollection<Chapter>>>(
                    chapterClasses.Select(it => new Tuple<string, ObservableCollection<Chapter>>(it.Key, new ObservableCollection<Chapter>(it.Value))));
            OnPropertyChanged(nameof(ChapterClasses));
            SelectedChapterClassIndex = 0;
            OnPropertyChanged(nameof(SelectedChapterClassIndex));
        }

        #endregion
    }
}
