using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public static partial class Constant
    {
        public const string TagNotFound = "该标签不存在";
    }

    class TagExplorerViewModel : ExplorerViewModel<TagDetailViewModel>
    {
        private ObservableCollection<TagType> TagTypes { get; }

        public TagExplorerViewModel()
        {
            TagTypes = Model.TagTypes;
        }

        public override async ValueTask OnLoaded()
        {
            await Model.ListTagTypes();
        }

        public override async ValueTask CreateNewTab(long tagId = -1)
        {
            if (tagId <= 0)
            {
                var tagDetail = new TagDetailViewModel()
                {
                    TagTypes = TagTypes,
                    ExplorerHeader = new ExplorerHeader()
                    {
                        Header = Constant.NewTagName,
                        CloseButtonEnabled = true
                    }
                };
                Explorer.Insert(Explorer.Count - 1, tagDetail);
            }
            else
            {
                var tag = await Model.GetTag(tagId);
                if (tag == null)
                {
                    MessageBox.Show(Constant.TagNotFound);
                    return;
                }
                var tagDetail = new TagDetailViewModel(tag, TagTypes);
                Explorer.Insert(Explorer.Count - 1, tagDetail);
            }
        }
    }
}
