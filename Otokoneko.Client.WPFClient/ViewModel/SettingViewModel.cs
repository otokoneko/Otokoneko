using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AsyncAwaitBestPractices.MVVM;
using ControlzEx.Theming;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public static partial class Constant
    {
        public const string ResetSettingNotice = "是否确定重置当前设置？";
    }

    class SettingViewModel : BaseViewModel
    {
        public Setting Setting => Model.Setting;

        public ObservableCollection<Tuple<Brush, AsyncCommand>> ThemeColors { get; set; }

        public string ThemeColor
        {
            get => Setting.ThemeOption.Color;
            set
            {
                Setting.ThemeOption.Color = value;
                Model.Setting.ThemeOption.Color = value;
                Model.ChangeTheme();
                Model.Setting = Setting;
            }
        }

        public OrderType OrderType
        {
            get => Setting.SearchOption.OrderType;
            set
            {
                Setting.SearchOption.OrderType = value;
                Model.Setting = Setting;
                OnPropertyChanged(nameof(OrderType));
            }
        }

        public bool Asc
        {
            get => Setting.SearchOption.Asc;
            set
            {
                Setting.SearchOption.Asc = value;
                Model.Setting = Setting;
            }
        }

        public int PageSize
        {
            get => Setting.SearchOption.PageSize;
            set
            {
                Setting.SearchOption.PageSize = value;
                Model.Setting = Setting;
            }
        }

        public WindowMode WindowMode
        {
            get => Setting.MangaReadOption.WindowOption.WindowMode;
            set
            {
                Setting.MangaReadOption.WindowOption.WindowMode = value;
                Model.Setting = Setting;
                OnPropertyChanged(nameof(WindowMode));
            }
        }

        public ImageDisplayMode ImageDisplayMode
        {
            get => Setting.MangaReadOption.ImageDisplayMode;
            set
            {
                Setting.MangaReadOption.ImageDisplayMode = value;
                Model.Setting = Setting;
                OnPropertyChanged(nameof(ImageDisplayMode));
            }
        }

        public AutoCropMode AutoCropMode
        {
            get => Setting.MangaReadOption.AutoCropMode;
            set
            {
                Setting.MangaReadOption.AutoCropMode = value;
                Model.Setting = Setting;
                OnPropertyChanged(nameof(AutoCropMode));
            }
        }

        public ScaleMode ScaleMode
        {
            get => Setting.MangaReadOption.ScaleMode;
            set
            {
                Setting.MangaReadOption.ScaleMode = value;
                Model.Setting = Setting;
                OnPropertyChanged(nameof(ScaleMode));
            }
        }

        public double ScaleValue
        {
            get => Setting.MangaReadOption.ScaleValue;
            set
            {
                if (value >= 0.1 && value <= 10)
                    Setting.MangaReadOption.ScaleValue = value;
                Model.Setting = Setting;
                OnPropertyChanged(nameof(ScaleMode));
            }
        }

        public string CacheUsage
        {
            get
            {
                var size = (double)Model.CacheSize;
                return Utils.FormatUtils.FormatSizeOfBytes(size);
            }
        }

        public ICommand ResetCommand => new AsyncCommand(async () =>
        {
            var result = MessageBox.Show(Constant.ResetSettingNotice, Constant.OperateNotice, MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;
            Model.Setting = new Setting();
            Model.ChangeTheme();
        });

        public ICommand ClearCacheCommand => new AsyncCommand(async () =>
        {
            Model.ClearCache();
            OnPropertyChanged(nameof(CacheUsage));
        });

        public ICommand ResetFtsIndexCommand => new AsyncCommand(async () =>
        {
            await Model.ResetFtsIndex();
        });

        public SettingViewModel()
        {
            ThemeColors = new ObservableCollection<Tuple<Brush, AsyncCommand>>();
            var colors = new HashSet<Color>();
            foreach (var theme in ThemeManager.Current.Themes)
            {
                if (colors.Contains(theme.PrimaryAccentColor)) continue;
                ThemeColors.Add(new Tuple<Brush, AsyncCommand>(new SolidColorBrush(theme.PrimaryAccentColor), new AsyncCommand(async () =>
                {
                    ThemeColor = theme.ColorScheme;
                })));
                colors.Add(theme.PrimaryAccentColor);
            }
        }
    }
}
