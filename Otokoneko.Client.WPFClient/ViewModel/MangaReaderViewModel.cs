using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.Client.WPFClient.Utils;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public static partial class Constant
    {
        public const string ChapterNotFound = "该章节不存在";
    }

    abstract class DisplayImageViewModel : BaseViewModel
    {
        public double ScaleValue { get; set; }
        public Action<int> SetProgress { get; private set; }
        public abstract void ScrollTo(int page);
        public abstract ObservableCollection<DisplayImage> Images { get; set; }

        public DisplayImageViewModel(double scaleValue, Action<int> setProgress)
        {
            ScaleValue = scaleValue;
            SetProgress = setProgress;
        }
    }

    class ImageListBoxViewModel: DisplayImageViewModel
    {
        public override void ScrollTo(int page)
        {
            page = MathUtils.LimitValue(page, 0, Images.Count - 1);
            SelectedItem = Images[page];
            OnPropertyChanged(nameof(SelectedItem));
        }

        public DisplayImage SelectedItem { get; set; }

        private ObservableCollection<DisplayImage> _images;
        public override ObservableCollection<DisplayImage> Images
        {
            get => _images;
            set
            {
                _images = value;
                foreach (var image in Images)
                {
                    image.ImageResizeMode = ImageResizeMode.RespectWidth;
                }
                OnPropertyChanged(nameof(Images));
            }
        }

        public ImageListBoxViewModel(double scaleValue, Action<int> setProgress) : base(scaleValue, setProgress) { }
    }

    class SingleImageViewModel: DisplayImageViewModel
    {
        public DisplayImage CurrentImage => Images != null && CurrentPage >= 0 && CurrentPage < Images.Count
            ? Images[CurrentPage]
            : null;

        private Func<double> GetHeight;

        private int _currentPage = -1;
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                var oldPage = _currentPage;
                _currentPage = Math.Max(0, Math.Min(Images.Count - 1, value));
                if (oldPage == _currentPage) return;
                OnPropertyChanged(nameof(CurrentImage));
                SetProgress(_currentPage + 1);
                if (oldPage >= 0 && oldPage < Images.Count)
                    Images[oldPage].RealSource = null;
            }
        }

        public override void ScrollTo(int page)
        {
            if (!Images[page].Visable)
            {
                return;
            }
            CurrentPage = page;
        }

        private ObservableCollection<DisplayImage> _images;
        public override ObservableCollection<DisplayImage> Images
        {
            get => _images;
            set
            {
                _images = value;
                var height = GetHeight();
                foreach(var image in Images)
                {
                    image.ExpectedHeight = height;
                    image.ImageResizeMode = ImageResizeMode.RespectHeight;
                }
                OnPropertyChanged(nameof(CurrentImage));
            }
        }

        public SingleImageViewModel(double scaleValue, Action<int> setProgress, Func<double> getHeight) : base(scaleValue, setProgress)
        {
            GetHeight = getHeight;
        }
    }

    class MangaReaderViewModel : BaseViewModel
    {
        #region Model

        private readonly List<Chapter> _chapters;
        private int _chapterIndex;
        private Chapter _chapter;

        #endregion

        #region DisplayMode

        public DisplayImageViewModel ImageExplorerViewModel { get; set; }

        private double _scaleValue;
        public double ScaleValue
        {
            get => _scaleValue;
            set
            {
                _scaleValue = value;
                if (ImageExplorerViewModel != null)
                    ImageExplorerViewModel.ScaleValue = value;
            }
        }
        public ObservableCollection<DisplayImage> Images { get; set; }
        public Transform Transform => new ScaleTransform(ScaleValue, ScaleValue);

        private AutoCropMode _autoCropMode;
        public AutoCropMode AutoCropMode
        {
            get => _autoCropMode;
            set
            {
                if (value == _autoCropMode) return;
                _autoCropMode = value;
                OnPropertyChanged(nameof(AutoCropMode));
                LoadChapter();
            }
        }

        private ImageDisplayMode _imageDisplayMode;

        public ImageDisplayMode ImageDisplayMode
        {
            get => _imageDisplayMode;
            set
            {
                _imageDisplayMode = value;
                OnPropertyChanged(nameof(ImageDisplayMode));
                ChangeImageDisplayMode();
            }
        }

        private void ChangeImageDisplayMode()
        {
            ImageExplorerViewModel = ImageDisplayMode switch
            {
                ImageDisplayMode.ImageListMode => new ImageListBoxViewModel(ScaleValue, SetProgress),
                ImageDisplayMode.SinglePageMode => new SingleImageViewModel(ScaleValue, SetProgress, GetHeight),
                _ => ImageExplorerViewModel
            };
            if (Images != null)
            {
                ImageExplorerViewModel.Images = Images;
                int page = (int)CurrentSliderValue;
                if (!Images[page].Visable)
                {
                    if (page > 0) page--;
                    else page++;
                }
                ImageExplorerViewModel.ScrollTo(page);
            }
            OnPropertyChanged(nameof(ImageExplorerViewModel));
        }

        #endregion

        #region Setting

        public bool MenuEnable { get; set; }

        public CircleButtonViewModel MenuButton => new CircleButtonViewModel()
        {
            Image = "/icon/list.png",
            IsEnable = true,
            Command = new AsyncCommand(async () =>
            {
                MenuEnable = !MenuEnable;
                OnPropertyChanged(nameof(MenuEnable));
            })
        };

        public CircleButtonViewModel FullScreenButton => new CircleButtonViewModel()
        {
            Image = WindowMode==WindowMode.FullScreen ? "/icon/resize-option.png" : "/icon/increase-size-option.png",
            IsEnable = true,
            Command = new AsyncCommand(async () =>
            {
                WindowMode = WindowMode == WindowMode.FullScreen ? WindowMode.NormalWindow : WindowMode.FullScreen;
            })
        };

        public CircleButtonViewModel NextChapterButton => new CircleButtonViewModel()
        {
            Image = "/icon/last.png",
            IsEnable = _chapterIndex < _chapters.Count - 1,
            Command = new AsyncCommand(async () =>
            {
                _chapterIndex++;
                await OnLoaded();
            })
        };

        public CircleButtonViewModel NextImageButton => new CircleButtonViewModel()
        {
            Image = "/icon/next.png",
            IsEnable = (int)CurrentSliderValue < Images?.Count || NextChapterButton.IsEnable,
            Command = new AsyncCommand(async () =>
            {
                int next = (int)CurrentSliderValue - 1 + 1;
                if (next < Images.Count && !Images[next].Visable)
                {
                    next++;
                }
                if (Images != null && next < Images.Count)
                {
                    ScrollTo(next);
                }
                else
                {
                    NextChapterButton.Command.Execute(null);
                }
            })
        };

        public CircleButtonViewModel PrevImageButton => new CircleButtonViewModel()
        {
            Image = "/icon/prev.png",
            IsEnable = (int)CurrentSliderValue > 1 || PrevChapterButton.IsEnable,
            Command = new AsyncCommand(async () =>
            {
                int next = (int)CurrentSliderValue - 1 - 1;
                if (next >= 0 && !Images[next].Visable)
                {
                    next--;
                }
                if (next >= 0)
                {
                    ScrollTo(next);
                }
                else
                {
                    PrevChapterButton.Command.Execute(null);
                }
            })
        };

        public CircleButtonViewModel PrevChapterButton => new CircleButtonViewModel()
        {
            Image = "/icon/first.png",
            IsEnable = _chapterIndex > 0,
            Command = new AsyncCommand(async () =>
            {
                _chapterIndex--;
                await OnLoaded();
            })
        };

        #endregion

        #region WindowMode

        public string Title { get; set; }

        public Func<double> GetWidth { get; set; }
        public Func<double> GetHeight { get; set; }
        public Func<double> GetTitleBarHeight { get; set; }

        private WindowMode _windowMode;
        public WindowMode WindowMode
        {
            get => _windowMode;
            set
            {
                _windowMode = value;
                ChangeWindowState();
            }
        }

        public bool ShowCloseButton { get; set; }
        public bool ShowMinButton { get; set; }
        public bool ShowMaxRestoreButton { get; set; }
        public bool ShowTitleBar { get; set; }
        public WindowState WindowState { get; set; }

        private void ChangeWindowState()
        {
            ShowCloseButton = WindowMode is WindowMode.NormalWindow or WindowMode.BorderlessWindowWithControlButton;
            ShowMinButton = WindowMode is WindowMode.NormalWindow or WindowMode.BorderlessWindowWithControlButton;
            ShowMaxRestoreButton = WindowMode is WindowMode.NormalWindow or WindowMode.BorderlessWindowWithControlButton;
            ShowTitleBar = WindowMode == WindowMode.NormalWindow;
            WindowState = WindowMode == WindowMode.FullScreen ? WindowState.Maximized : WindowState.Normal;
            OnPropertyChanged(nameof(ShowCloseButton));
            OnPropertyChanged(nameof(ShowMinButton));
            OnPropertyChanged(nameof(ShowTitleBar));
            OnPropertyChanged(nameof(ShowMaxRestoreButton));
            OnPropertyChanged(nameof(WindowState));
            OnPropertyChanged(nameof(FullScreenButton));
            OnPropertyChanged(nameof(WindowMode));
        }

        #endregion

        #region Zoom

        public ICommand ZoomCommand => new AsyncCommand<double>(async (delta) =>
        {
            ScaleValue += delta;
            ScaleValue = Math.Max(ScaleValue, 0.1);
            ScaleValue = Math.Min(10, ScaleValue);
            OnPropertyChanged(nameof(ScaleValue));
            OnPropertyChanged(nameof(Transform));
        });

        public ICommand FixWidthCommand => new AsyncCommand(async () =>
        {
            ScaleValue = GetWidth() / CurrentImage.ActualWidth;
            ScaleValue = Math.Max(ScaleValue, 0.1);
            ScaleValue = Math.Min(10, ScaleValue);
            OnPropertyChanged(nameof(ScaleValue));
            OnPropertyChanged(nameof(Transform));
        });

        public ICommand FixHeightCommand => new AsyncCommand(async () =>
        {
            ScaleValue = (GetHeight() - (ShowTitleBar ? GetTitleBarHeight() : 0)) / CurrentImage.ActualHeight;
            ScaleValue = Math.Max(ScaleValue, 0.1);
            ScaleValue = Math.Min(10, ScaleValue);
            OnPropertyChanged(nameof(ScaleValue));
            OnPropertyChanged(nameof(Transform));
        });

        public void Zoom(double delta)
        {
            ScaleValue += (delta / 1200);
            ScaleValue = Math.Max(ScaleValue, 0.1);
            ScaleValue = Math.Min(10, ScaleValue);
            OnPropertyChanged(nameof(ScaleValue));
            OnPropertyChanged(nameof(Transform));
        }

        #endregion

        #region Progress

        public void ScrollTo(int page)
        {
            ImageExplorerViewModel?.ScrollTo(page);
        }
        public void SetProgress(int progress)
        {
            _currentSliderValue = progress;
            SliderText = $"{(progress + 1) / 2}/{_chapter?.Images?.Count}";
            OnPropertyChanged(nameof(SliderText));
            OnPropertyChanged(nameof(CurrentSliderValue));
            OnPropertyChanged(nameof(PrevImageButton));
            OnPropertyChanged(nameof(NextImageButton));
        }

        public string SliderText { get; set; }
        private double _currentSliderValue;
        public double CurrentSliderValue
        {
            get => _currentSliderValue;
            set
            {
                if(_currentSliderValue == value) return;
                _currentSliderValue = value;
                ScrollTo((int)value - 1);
            }
        }

        public DisplayImage CurrentImage
        {
            get
            {
                if ((int)CurrentSliderValue >= 1 && (int)CurrentSliderValue <= Images.Count)
                {
                    return Images[(int)CurrentSliderValue - 1];
                }

                return Images[0];
            }
        }

        private async ValueTask SaveProgress()
        {
            Image image = CurrentImage.Image;
            var progress = _chapter.Images.IndexOf(image) * 2;
            if ((int)CurrentSliderValue - 2 >= 0 && Images[(int)CurrentSliderValue - 2].Image == image)
            {
                progress++;
            }
            await Model.RecordReadProgress(_chapter.ObjectId, progress);
        }

        private async ValueTask LoadProgress()
        {
            var index = 0;
            if (_chapter.ReadProgress != null)
            {
                index = Images.IndexOf(Images.FirstOrDefault(it =>
                    it.Image == _chapter.Images[_chapter.ReadProgress.Progress / 2]));
                if (AutoCropMode != AutoCropMode.None && _chapter.ReadProgress.Progress % 2 == 1)
                {
                    index++;
                }
            }

            ScrollTo(index);
        }

        #endregion

        private void LoadImages()
        {
            Images = new ObservableCollection<DisplayImage>();
            foreach (var image in _chapter.Images)
            {
                var w = 0.8 * GetWidth();
                var h = 1.5 * w;

                switch (AutoCropMode)
                {
                    case AutoCropMode.None:
                    case AutoCropMode.RightToLeft:
                        Images.Add(new DisplayImage(
                            image,
                            true,
                            w,
                            h, 
                            AutoCropMode,
                            ImageCropMode.Right,
                            ImageResizeMode.RespectWidth));
                        Images.Add(new DisplayImage(
                            image,
                            true,
                            w,
                            h,
                            AutoCropMode,
                            ImageCropMode.Left,
                            ImageResizeMode.RespectWidth));
                        break;
                    case AutoCropMode.LeftToRight:
                        Images.Add(new DisplayImage(
                            image,
                            true,
                            w,
                            h,
                            AutoCropMode,
                            ImageCropMode.Left,
                            ImageResizeMode.RespectWidth));
                        Images.Add(new DisplayImage(
                            image,
                            true,
                            w,
                            h,
                            AutoCropMode,
                            ImageCropMode.Right,
                            ImageResizeMode.RespectWidth));
                        break;
                }
            }

            ImageExplorerViewModel.Images = Images;

            switch (Model.Setting.MangaReadOption.ScaleMode)
            {
                case ScaleMode.FitWidth:
                    FixWidthCommand.Execute(null);
                    break;
                case ScaleMode.FitHeight:
                    FixHeightCommand.Execute(null);
                    break;
            }
        }

        private async ValueTask LoadChapter()
        {
            if (_chapterIndex < 0 || _chapterIndex >= _chapters.Count) return;
            if (_chapter != null) await SaveProgress();
            _chapter = await Model.GetChapter(_chapters[_chapterIndex].ObjectId);
            if (_chapter == null)
            {
                MessageBox.Show(Constant.ChapterNotFound);
                return;
            }
            Title = _chapter.Title;
            LoadImages();
            await LoadProgress();
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(ImageDisplayMode));
            OnPropertyChanged(nameof(PrevChapterButton));
            OnPropertyChanged(nameof(NextChapterButton));
            OnPropertyChanged(nameof(PrevImageButton));
            OnPropertyChanged(nameof(NextImageButton));
        }

        public async ValueTask OnLoaded()
        {
            await LoadChapter();
        }

        public async ValueTask OnClosed()
        {
            if (_chapter != null)
                await SaveProgress();
        }

        public MangaReaderViewModel(List<Chapter> chapters, int index)
        {
            WindowMode = Model.Setting.MangaReadOption.WindowOption.WindowMode;
            _autoCropMode = Model.Setting.MangaReadOption.AutoCropMode;
            _chapters = chapters;
            _chapterIndex = index;
            ScaleValue = Model.Setting.MangaReadOption.ScaleValue;
            ImageDisplayMode = Model.Setting.MangaReadOption.ImageDisplayMode;
        }
    }
}