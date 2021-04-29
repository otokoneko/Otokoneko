using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    class TagSelectionViewModel : BaseViewModel
    {
        private readonly TagQueryHelper _query;
        private bool _loaded = false;
        private int _loading = 0;
        public string Keyword { get; set; }
        public ObservableCollection<DisplayTag> SearchResult { get; set; }
        public ObservableCollection<DisplayTag> SelectedTags { get; set; }

        #region QuicklyCreateTag

        public ObservableCollection<TagType> TagTypes => Model.TagTypes;

        public ICommand QuicklyCreateTag { get; } = new AsyncCommand<object[]>(async parameter =>
        {
            var tagType = (TagType)parameter[0];
            var tagName = (string)parameter[1];

            if (tagType == null)
            {
                MessageBox.Show(Constant.ShouldSelectTagType);
                return;
            }
            if (string.IsNullOrEmpty(tagName))
            {
                MessageBox.Show(Constant.TagNameShouldNotBeEmpty);
                return;
            }

            var success = await Model.AddTag(new Tag() {Name = tagName, TypeId = tagType.ObjectId});
            MessageBox.Show(success != null ? Constant.AddTagSuccess : Constant.AddTagFail);
        });

        #endregion

        public List<Tag> GetResult()
        {
            return SelectedTags.Select(it => new Tag() { Name = it.Name, ObjectId = it.ObjectId, TypeId = it.TypeId, Key = it.Key }).ToList();
        }

        public TagSelectionViewModel(long typeId, List<Tag> selectedTags)
        {
            _query = new TagQueryHelper()
            {
                Limit = 100,
                Offset = 0,
                QueryString = null,
                TypeId = typeId
            };
            SelectedTags = new ObservableCollection<DisplayTag>();
            if (selectedTags == null) return;
            foreach (var selectedTag in selectedTags)
            {
                var displayTag = new DisplayTag(selectedTag);
                displayTag.ClickCommand = new AsyncCommand(async () =>
                {
                    SelectedTags.Remove(displayTag);
                });
                SelectedTags.Add(displayTag);
            }
        }

        public async ValueTask OnLoaded()
        {
            await Search();
        }

        public ICommand SearchCommand => new AsyncCommand(async () =>
        {
            await Search();
        });

        private async ValueTask Search()
        {
            SearchResult = new ObservableCollection<DisplayTag>();
            OnPropertyChanged(nameof(SearchResult));
            _query.QueryString = Keyword;
            _query.Offset = 0;
            _loaded = false;
            await LoadTags();
        }

        public async ValueTask LoadTags()
        {
            try
            {
                if (Interlocked.Increment(ref _loading) != 1) return;
                if (_loaded) return;
                var tags = await Model.ListTags(_query.QueryString, _query.TypeId, _query.Offset, _query.Limit);
                if (tags != null && tags.Count > 0)
                {
                    _query.Offset += _query.Limit;
                    foreach (var tag in tags.Where(it => SelectedTags.All(tag => tag.ObjectId != it.ObjectId)))
                    {
                        var displayTag = new DisplayTag(tag);
                        displayTag.ClickCommand = new AsyncCommand(async () =>
                        {
                            if (SelectedTags.All(it => it.ObjectId != displayTag.ObjectId))
                            {
                                displayTag.ClickCommand = new AsyncCommand(async () =>
                                {
                                    SelectedTags.Remove(displayTag);
                                });
                                SelectedTags.Add(displayTag);
                            }
                        });
                        SearchResult.Add(displayTag);
                    }
                }
                else
                {
                    _loaded = true;
                }
            }
            finally
            {
                Interlocked.Decrement(ref _loading);
            }
        }
    }
}
