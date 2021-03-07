using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.Client.WPFClient.View;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    class TagManagerViewModel: BaseViewModel
    {
        private readonly TagQueryHelper _query;

        public string SearchKeywords { get; set; }
        public CircleButtonViewModel SearchCircleButton => new CircleButtonViewModel()
        {
            IsEnable = true,
            Image = "/icon/search.png",
            Command = SearchCommand
        };

        public ICommand SearchCommand => new AsyncCommand(async () =>
        {
            _query.QueryString = SearchKeywords;
            _query.TypeId = TagTypes[SelectedTagTypeIndex].ObjectId;
            Tags.Clear();
            _tagLoaded = false;
            await LoadTags();
        });

        public TagExplorerViewModel TagExplorerViewModel { get; set; }
        public ObservableCollection<DisplayTag> Tags { get; set; }

        private int _selectedTagTypeIndex = -1;
        public int SelectedTagTypeIndex
        {
            get => _selectedTagTypeIndex;
            set
            {
                _selectedTagTypeIndex = value;
                if (_selectedTagTypeIndex != TagTypes.Count - 1) return;
                var tagTypeManager = new TagTypeManager();
                tagTypeManager.ShowDialog();
            }
        }

        public ObservableCollection<TagType> TagTypes { get; set; }
        private readonly ObservableCollection<TagType> _tagTypes;

        private bool _tagLoaded = false;
        private bool _loaded = false;
        private int _loadTagLock = 0;

        public ICommand LoadTagsCommand => new AsyncCommand(async () => await LoadTags());

        public TagManagerViewModel()
        {
            TagExplorerViewModel = new TagExplorerViewModel();
            _tagTypes = Model.TagTypes;
            _tagTypes.CollectionChanged += (_, __) => TagTypesOnCollectionChanged();
            _query = new TagQueryHelper()
            {
                QueryString = null,
                TypeId = -1,
                Offset = 0,
                Limit = 100
            };
        }

        private void TagTypesOnCollectionChanged()
        {
            TagTypes = new ObservableCollection<TagType>();
            var allTagType = new TagType()
            {
                Name = "全部",
                ObjectId = -1
            };
            var manageTagType = new TagType()
            {
                Name = "管理标签类别...",
                ObjectId = long.MaxValue
            };
            TagTypes.Add(allTagType);
            foreach (var tagType in _tagTypes)
            {
                TagTypes.Add(tagType);
            }
            TagTypes.Add(manageTagType);
            OnPropertyChanged(nameof(TagTypes));
            SelectedTagTypeIndex = 0;
            OnPropertyChanged(nameof(SelectedTagTypeIndex));
        }

        public async ValueTask LoadTagTypes()
        {
            await Model.ListTagTypes();
        }

        public async ValueTask LoadTags()
        {
            try
            {
                if (Interlocked.Increment(ref _loadTagLock) != 1) return;
                if (_tagLoaded) return;

                var tagTypeDict = _tagTypes.ToDictionary(it => it.ObjectId, it => it);

                var tags = await Model.ListTags(_query.QueryString, _query.TypeId, Tags.Count, _query.Limit);
                if (tags == null || tags.Count == 0)
                {
                    _tagLoaded = true;
                    return;
                }

                foreach (var tag in tags)
                {
                    Tags.Add(new DisplayTag(tag)
                    {
                        Color = tagTypeDict[tag.TypeId].Color,
                        Name = $"{tagTypeDict[tag.TypeId].Name}: {tag.Name}",
                        ClickCommand = new AsyncCommand(async () => { await TagExplorerViewModel.CreateNewTab(tag.ObjectId); })
                    });
                }
            }
            finally
            {
                Interlocked.Decrement(ref _loadTagLock);
            }
        }

        public async ValueTask OnLoaded()
        {
            if(_loaded) return;
            _loaded = true;
            Tags = new ObservableCollection<DisplayTag>();
            OnPropertyChanged(nameof(Tags));
            TagTypesOnCollectionChanged();
            await LoadTagTypes();
            await LoadTags();
        }
    }
}
