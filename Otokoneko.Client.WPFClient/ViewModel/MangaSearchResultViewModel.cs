using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public class MangaSearchResultState : INavigationState
    {
        public double VerticalOffset { get; set; }
        public int InitPage { get; set; }
        public MangaQueryHelper Query { get; set; }
        public NavigationService NavigationService { get; set; }

        public INavigationViewModel GetViewModel()
        {
            return new MangaSearchResultViewModel()
            {
                InitVerticalOffset = VerticalOffset,
                InitPage = InitPage,
                Query = Query,
                NavigationService = NavigationService
            };
        }
    }

    public class MangaSearchResultViewModel : BaseViewModel, INavigationViewModel
    {
        public NavigationService NavigationService { get; set; }

        private bool Loaded { get; set; } = false;

        public double VerticalOffset { get; set; }
        public Action ScrollToTop { get; set; }

        public double InitVerticalOffset { get; set; }
        public int InitPage { get; set; }

        public int TotalPage { get; set; }
        public int CurrentPage { get; set; }

        public bool FilterEnable { get; set; }

        public OrderType OrderType
        {
            get => Query.OrderType;
            set
            {
                Query.OrderType = value;
                OnPropertyChanged(nameof(OrderType));
                InitPage = 1;
                Search();
            }
        }

        public bool Asc
        {
            get => Query.Asc;
            set
            {
                Query.Asc = value;
                OnPropertyChanged(nameof(Asc));
                InitPage = 1;
                Search();
            }
        }

        public MangaQueryHelper Query { get; set; }

        public CircleButtonViewModel FilterCircleButton => new CircleButtonViewModel()
        {
            IsEnable = true,
            Image = "/icon/filter.png",
            Command = new AsyncCommand(async () =>
            {
                FilterEnable = !FilterEnable;
                OnPropertyChanged(nameof(FilterEnable));
            })
        };

        public CircleButtonViewModel FirstCircleButton => new CircleButtonViewModel()
        {
            IsEnable = true,
            Image = "/icon/first.png",
            Command = FirstPageCommand
        };

        public CircleButtonViewModel LastCircleButton => new CircleButtonViewModel()
        {
            IsEnable = true,
            Image = "/icon/last.png",
            Command = LastPageCommand
        };

        public CircleButtonViewModel PrevCircleButton => new CircleButtonViewModel()
        {
            IsEnable = true,
            Image = "/icon/prev.png",
            Command = PrevPageCommand
        };

        public CircleButtonViewModel NextCircleButton => new CircleButtonViewModel()
        {
            IsEnable = true,
            Image = "/icon/next.png",
            Command = NextPageCommand
        };

        public ICommand FirstPageCommand => new AsyncCommand(async () =>
        {
            await LoadCurrentPage(1);
        });

        public ICommand LastPageCommand => new AsyncCommand(async () =>
        {
            await LoadCurrentPage(TotalPage);
        });

        public ICommand PrevPageCommand => new AsyncCommand(async () =>
        {
            await LoadCurrentPage(Math.Max(1, CurrentPage - 1));
        });

        public ICommand NextPageCommand => new AsyncCommand(async () =>
        {
            await LoadCurrentPage(Math.Min(TotalPage, CurrentPage + 1));
        });

        public ICommand ChangePageCommand => new AsyncCommand<string>(async page =>
        {
            if (int.TryParse(page, out var p))
            {
                await LoadCurrentPage(Math.Max(1, Math.Min(p, TotalPage)));
            }
        });

        private async Task LoadCurrentPage(int page)
        {
            if (CurrentPage == page) return;
            CurrentPage = page;
            var mangas = await Model.ListMangas(new MangaQueryHelper()
            {
                Asc = Query.Asc,
                Limit = Query.Limit,
                Offset = (CurrentPage - 1) * Query.Limit,
                OrderType = Query.OrderType,
                QueryString = Query.QueryString,
                QueryType = Query.QueryType
            });
            if (mangas == null) return;

            for (int i = 0; i < mangas.Count; i++)
            {
                var manga = mangas[i];
                var displayManga = new DisplayManga()
                {
                    ObjectId = manga.ObjectId,
                    Title = manga.Title,
                    CoverId = manga.CoverId,
                    Description =
                        $"{manga.Title.Trim('\n')}\n\n{(manga.Description != null ? manga.Description.Trim('\n') : "")}",
                    ClickCommand = new AsyncCommand(async () =>
                    {
                        NavigationService.Navigate(new MangaDetailViewModel(NavigationService, manga));
                    })
                };
                if (i >= Mangas.Count)
                    Mangas.Add(displayManga);
                else
                    Mangas[i] = displayManga;
            }

            while(mangas.Count < Mangas.Count)
            {
                Mangas.RemoveAt(Mangas.Count - 1);
            }

            OnPropertyChanged(nameof(CurrentPage));

            ScrollToTop?.Invoke();
        }

        public ObservableCollection<DisplayManga> Mangas { get; } = new ObservableCollection<DisplayManga>();

        public INavigationState GetState()
        {
            return new MangaSearchResultState()
            {
                InitPage = CurrentPage,
                VerticalOffset = VerticalOffset,
                Query = Query,
                NavigationService = NavigationService
            };
        }

        public MangaSearchResultViewModel(NavigationService navigationService, string queryString, QueryType queryType)
        {
            NavigationService = navigationService;
            InitPage = 1;
            var searchOption = Model.Setting.SearchOption;
            Query = new MangaQueryHelper()
            {
                QueryString = queryString,
                QueryType = queryType,
                Asc = queryType != QueryType.Keyword || !string.IsNullOrEmpty(queryString?.Trim()) || searchOption.Asc,
                Limit = searchOption.PageSize,
                Offset = 0,
                OrderType = queryType == QueryType.Keyword && string.IsNullOrEmpty(queryString?.Trim())
                    ? searchOption.OrderType
                    : OrderType.Default,
            };
        }

        public MangaSearchResultViewModel() { }

        private async ValueTask Search()
        {
            var count = await Model.CountMangas(new MangaQueryHelper()
            {
                OrderType = OrderType.Default,
                QueryString = Query.QueryString,
                QueryType = Query.QueryType
            });
            TotalPage = (int)Math.Ceiling((double)count / Query.Limit);
            OnPropertyChanged(nameof(TotalPage));
            CurrentPage = 0;
            await LoadCurrentPage(InitPage);
        }

        public async ValueTask OnLoaded()
        {
            if (Loaded) return;
            Loaded = true;
            await Search();
        }
    }
}