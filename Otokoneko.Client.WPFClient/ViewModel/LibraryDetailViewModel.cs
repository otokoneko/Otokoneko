using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public static partial class Constant
    {
        public const string LibraryNotFound = "所选择的库不存在";

        public const string LibraryNameShouldNotBeEmpty = "库名称不可为空";
        public const string LibraryPathShouldNotBeEmpty = "库路径不可为空";
        public const string AddLibrarySuccess = "添加库成功";
        public const string AddLibraryFail = "添加库失败";

        public const string DeleteLibraryNotice = "删除库的同时将会移除相关漫画，是否继续？";

        public const string FileScheme = "file";
    }

    class LibraryDetailViewModel : BaseViewModel
    {
        private FileTreeRoot _library;

        public long ObjectId { get; set; }

        public bool IsNewLibrary { get; set; }

        public string Name { get; set; }
        public string Path { get; set; }

        public ObservableCollection<string> Scrapers { get; set; }

        public int SelectedScraperIndex
        {
            get => Scrapers != null ? Math.Max(0, Scrapers.IndexOf(_library.ScraperName)) : -1;
            set => _library.ScraperName = (value > 0 && value < Scrapers?.Count) ? Scrapers[value] : null;
        }

        public LibraryDetailViewModel(long libraryId)
        {
            ObjectId = libraryId;
            IsNewLibrary = libraryId <= 0;
        }

        public async ValueTask OnLoaded()
        {
            if (ObjectId > 0)
            {
                _library = await Model.GetLibrary(ObjectId);
                if (_library == null)
                {
                    MessageBox.Show(Constant.LibraryNotFound);
                    ObjectId = -1;
                    IsNewLibrary = true;
                    OnPropertyChanged(nameof(IsNewLibrary));
                    return;
                }
            }
            else
            {
                _library = new FileTreeRoot() {Scheme = Constant.FileScheme};
            }
            
            Name = _library.Name;
            Path = _library.Path;

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Path));

            var scrapers = await Model.GetScrapers();
            Scrapers = new ObservableCollection<string>(scrapers);
            Scrapers.Insert(0, Constant.Null);
            OnPropertyChanged(nameof(Scrapers));
            OnPropertyChanged(nameof(SelectedScraperIndex));
        }

        private bool Check()
        {
            if (string.IsNullOrEmpty(Name?.Trim()))
            {
                MessageBox.Show(Constant.PlanNameShouldNotBeEmpty);
                return false;
            }
            if (string.IsNullOrEmpty(Path?.Trim()))
            {
                MessageBox.Show(Constant.LibraryPathShouldNotBeEmpty);
                return false;
            }

            return true;
        }

        public ICommand SaveCommand => new AsyncCommand(async () =>
        {
            if (!Check()) return;
            _library.Name = Name;
            _library.Path = Path;
            if (_library.ObjectId > 0)
            {
                var success = await Model.UpdateLibrary(_library);
                MessageBox.Show(success ? Constant.UpdateSuccess : Constant.UpdateFail);
            }
            else
            {
                var result = await Model.AddLibrary(_library);
                if (result != null)
                {
                    MessageBox.Show(Constant.AddLibrarySuccess);
                    _library = result;
                    ObjectId = _library.ObjectId;
                    IsNewLibrary = false;
                    OnPropertyChanged(nameof(IsNewLibrary));
                }
                else
                {
                    MessageBox.Show(Constant.AddLibraryFail);
                }
            }
        });

        public ICommand ScanLibraryCommand => new AsyncCommand(async () =>
        {
            if (ObjectId > 0)
            {
                await Model.ScanLibraryAndCreateMangaItems(ObjectId);
            }
        });

        public ICommand DeleteCommand => new AsyncCommand(async () =>
        {
            if (ObjectId > 0)
            {
                var result = MessageBox.Show(Constant.DeleteLibraryNotice, Constant.OperateNotice, MessageBoxButton.YesNo);
                if (result != MessageBoxResult.Yes) return;
                await Model.DeleteLibrary(ObjectId);
            }
        });
    }
}
