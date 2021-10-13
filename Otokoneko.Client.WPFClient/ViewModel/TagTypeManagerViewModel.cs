using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    class TagTypeManagerViewModel: BaseViewModel
    {
        public ObservableCollection<DisplayTagType> TagTypes { get; set; }

        public TagTypeManagerViewModel()
        {
            Model.TagTypes.CollectionChanged += (_, __) => OnTagTypesChanged();
        }

        private void OnTagTypesChanged()
        {
            TagTypes = new ObservableCollection<DisplayTagType>();
            foreach (var tagType in Model.TagTypes)
            {
                TagTypes.Add(new DisplayTagType(tagType));
            }
            TagTypes.Add(new DisplayTagType(new TagType()
            {
                ObjectId = -1,
                Name = null
            }));
            OnPropertyChanged(nameof(TagTypes));
        }

        public async ValueTask OnLoaded()
        {
            OnTagTypesChanged();
            await Model.ListTagTypes();
        }
    }
}
