using System.ComponentModel;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public class MangaExplorerViewModel : BaseViewModel
    {
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
            Command = NavigationService.SearchService.SearchCommand
        };

        public void RemoveSearchHistory(string keywords)
        {
            Model.RemoveMangaSearchHistory(keywords);
        }

        public void SetKeyword(string keywords)
        {
            NavigationService.SearchService.SetKeyword(keywords);
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