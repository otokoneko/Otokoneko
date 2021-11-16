using AsyncAwaitBestPractices.MVVM;
using Otokoneko.DataType;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public class SearchServiceViewModel : BaseViewModel
    {
        public SearchServiceViewModel(NavigationService navigationService)
        {
            NavigationService = navigationService;
        }

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
                        _searchKeywords = _searchKeywords[..(_searchKeywords.LastIndexOf('$') + 1)] +
                        (typeName != null ? $"{typeName}:{tag.Name}$" : $"{tag.Name}$");
                        OnPropertyChanged(nameof(SearchKeywords));
                    })
                };
                SearchHelper.Add(displayTag);
            }
            OnPropertyChanged(nameof(SearchHelper));
        }

        public void SetKeyword(string keywords)
        {
            _searchKeywords = keywords;
            OnPropertyChanged(nameof(SearchKeywords));
        }

        public void NewSearch(string keywords)
        {
            SetKeyword(keywords);
            if (string.IsNullOrEmpty(keywords?.Trim())) return;
            Model.AddMangaSearchHistory(keywords);
        }

        public ICommand SearchCommand => new AsyncCommand(async () =>
        {
            Keyboard.ClearFocus();
            NewSearch(SearchKeywords);
            NavigationService.Navigate(new MangaSearchResultViewModel(NavigationService, SearchKeywords, QueryType.Keyword));
        });

        public NavigationService NavigationService { get; }
    }
}
