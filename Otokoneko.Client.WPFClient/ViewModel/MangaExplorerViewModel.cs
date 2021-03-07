using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public class MangaExplorerViewModel : BaseViewModel
    {
        private string _searchKeywords;

        public string SearchKeywords
        {
            get => _searchKeywords;
            set
            {
                if (value == _searchKeywords) return;
                _searchKeywords = value;
                ShowSearchHelper();
            }
        }

        public ObservableCollection<string> SearchHistory => Model.MangaSearchHistory;

        public ObservableCollection<DisplayTag> SearchHelper { get; set; }

        private async Task ShowSearchHelper()
        {
            if (_searchKeywords == null)
            {
                return;
            }
            int count = 0, s = 0;
            for (var i = 0; i < _searchKeywords.Length; i++)
            {
                var ch = _searchKeywords[i];
                if (ch != '$') continue;
                count++;
                s = i;
            }
            if (count % 2 == 0 || _searchKeywords.Length - 1 - s <= 0)
            {
                return;
            }
            var keyword = _searchKeywords.Substring(s + 1, _searchKeywords.Length - 1 - s);
            Model.ListTagTypes();
            var tags = await Model.ListTags(keyword, -1, 0, 50);
            SearchHelper = new ObservableCollection<DisplayTag>();
            foreach (var tag in tags)
            {
                var displayTag = new DisplayTag(tag)
                {
                    ClickCommand = new AsyncCommand(async () =>
                    {
                        var typeName = Model.TagTypes.FirstOrDefault(it => it.ObjectId == tag.TypeId)?.Name;
                        _searchKeywords = typeName != null
                            ? _searchKeywords.Substring(0, _searchKeywords.LastIndexOf('$') + 1) + typeName + ":" + tag.Name + "$"
                            : _searchKeywords.Substring(0, _searchKeywords.LastIndexOf('$') + 1) + tag.Name + "$";
                        OnPropertyChanged(nameof(SearchKeywords));
                    })
                };
                SearchHelper.Add(displayTag);
            }
            OnPropertyChanged(nameof(SearchHelper));
        }

        public CircleButtonViewModel BackwardCircleButton { get; set; } 

        public CircleButtonViewModel ForwardCircleButton { get; set; }

        public CircleButtonViewModel RefreshCircleButton => new CircleButtonViewModel()
        {
            IsEnable = true,
            Image = "/icon/refresh.png",
            Command = new AsyncCommand(async () =>
            {
                NavigationService.Refresh();
            })
        };

        public CircleButtonViewModel HomeCircleButton => new CircleButtonViewModel()
        {
            IsEnable = true,
            Image = "/icon/home.png",
            Command = new AsyncCommand(async () =>
            {
                NavigationService.Navigate(new MangaSearchResultViewModel(NavigationService, null, QueryType.Keyword));
            })
        };

        public CircleButtonViewModel FavoriteCircleButton => new CircleButtonViewModel()
        {
            IsEnable = true,
            Image = "/icon/favorite.png",
            Command = new AsyncCommand(async () =>
            {
                NavigationService.Navigate(new MangaSearchResultViewModel(NavigationService, null, QueryType.Favorite));
            })
        };

        public CircleButtonViewModel HistoryCircleButton => new CircleButtonViewModel()
        {
            IsEnable = true,
            Image = "/icon/history.png",
            Command = new AsyncCommand(async () =>
            {
                NavigationService.Navigate(new MangaSearchResultViewModel(NavigationService, null, QueryType.History));
            })
        };

        public CircleButtonViewModel SearchCircleButton => new CircleButtonViewModel()
        {
            IsEnable = true,
            Image = "/icon/search.png",
            Command = SearchCommand
        };

        public ICommand SearchCommand => new AsyncCommand(async () =>
        {
            Keyboard.ClearFocus();
            if (!string.IsNullOrEmpty(SearchKeywords?.Trim())) Model.AddMangaSearchHistory(SearchKeywords);
            NavigationService.Navigate(new MangaSearchResultViewModel(NavigationService, SearchKeywords, QueryType.Keyword));
        });

        public void RemoveSearchHistory(string keywords)
        {
            Model.RemoveMangaSearchHistory(keywords);
        }

        public void SetKeyword(string keywords)
        {
            _searchKeywords = keywords;
            OnPropertyChanged(nameof(SearchKeywords));
        }

        public NavigationService NavigationService { get; set; }

        public MangaExplorerViewModel()
        {
            NavigationService = new NavigationService();

            BackwardCircleButton = new CircleButtonViewModel()
            {
                IsEnable = NavigationService.BackwardEnable,
                Image = "/icon/left-arrow.png",
                Command = new AsyncCommand(async () =>
                {
                    NavigationService.NavigateBack();
                })
            };
            ForwardCircleButton = new CircleButtonViewModel()
            {
                IsEnable = NavigationService.ForwardEnable,
                Image = "/icon/right-arrow.png",
                Command = new AsyncCommand(async () =>
                {
                    NavigationService.NavigateForward();
                })
            };

            NavigationService.Navigate(new MangaSearchResultViewModel(NavigationService, null, QueryType.Keyword));
            NavigationService.PropertyChanged += NavigationServiceOnPropertyChanged;
        }

        private void NavigationServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NavigationService.ForwardEnable))
            {
                ForwardCircleButton.IsEnable = NavigationService.ForwardEnable;
            }

            if (e.PropertyName == nameof(NavigationService.BackwardEnable))
            {
                BackwardCircleButton.IsEnable = NavigationService.BackwardEnable;
            }
        }
    }
}