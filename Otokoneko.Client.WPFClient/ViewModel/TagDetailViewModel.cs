using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.Client.WPFClient.View;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public static partial class Constant
    {
        public const string NewTagName = "新标签";

        public const string AddTagSuccess = "添加标签成功";
        public const string AddTagFail = "添加标签失败，请检查是否存在同类型且同名的标签。";

        public const string TagNameShouldNotBeEmpty = "标签名不可为空";
        public const string ShouldSelectTagType = "请选择一个标签类型";
    }

    class TagDetailViewModel : ExplorerContent
    {
        public Tag Tag { get; set; }

        public string Name { get; set; } = Constant.NewTagName;
        public string Detail { get; set; }

        public bool Editable { get; set; }

        public ObservableCollection<TagType> TagTypes { get; set; }
        public int SelectedTypeIndex { get; set; }

        public ObservableCollection<DisplayTag> Aliases { get; set; }

        public ICommand EditCommand => new AsyncCommand(async () =>
        {
            Editable = true;
            OnPropertyChanged(nameof(Editable));
            DisplayAliases();
        });

        public ICommand SaveCommand => new AsyncCommand(async () =>
        {
            if (!Check()) return;
            Editable = false;
            OnPropertyChanged(nameof(Editable));
            DisplayAliases();
            bool success;
            Tag.Name = Name;
            Tag.Detail = Detail;
            Tag.TypeId = TagTypes[SelectedTypeIndex].ObjectId;
            if (Tag.ObjectId > 0)
            {
                var result = await Model.UpdateTag(Tag);
                success = result;
                MessageBox.Show(result ? Constant.UpdateSuccess : Constant.UpdateFail);
            }
            else
            {
                Tag = await Model.AddTag(Tag);
                success = Tag != null;
                MessageBox.Show(Tag != null ? Constant.AddTagSuccess : Constant.AddTagFail);
            }
            if (success)
            {
                ExplorerHeader.Header = Name;
                OnPropertyChanged(nameof(ExplorerHeader));
            }
        });

        private ICommand ChangedAliasesCommand => new AsyncCommand(async () =>
        {
            if (!Check()) return;
            var tagSelection = new TagSelectionWindow(TagTypes[SelectedTypeIndex].ObjectId, Tag.Aliases);
            var result = tagSelection.ShowDialog();
            if (result == true)
            {
                Tag.Aliases = tagSelection.SelectedTags.Where(it => it.ObjectId != Tag.ObjectId).ToList();
                DisplayAliases();
            }
        });

        private bool Check()
        {
            if (string.IsNullOrEmpty(Name?.Trim()))
            {
                MessageBox.Show(Constant.TagNameShouldNotBeEmpty);
                return false;
            }

            if (SelectedTypeIndex < 0 || SelectedTypeIndex >= TagTypes.Count)
            {
                MessageBox.Show(Constant.ShouldSelectTagType);
                return false;
            }

            return true;
        }

        private void DisplayAliases()
        {
            Aliases = new ObservableCollection<DisplayTag>();
            if (Tag?.Aliases != null)
            {
                foreach (var tagAlias in Tag.Aliases)
                {
                    Aliases.Add(new DisplayTag(tagAlias));
                }
            }

            if (Editable)
            {
                Aliases.Add(new DisplayTag()
                {
                    Name = "+",
                    Color = Color.FromRgb(0x50, 0x50, 0x50),
                    ClickCommand = ChangedAliasesCommand
                });
            }
            OnPropertyChanged(nameof(Aliases));
        }

        public TagDetailViewModel(Tag tag, ObservableCollection<TagType> tagTypes)
        {
            Tag = tag;
            Name = tag.Name;
            Detail = tag.Detail;
            Editable = false;
            TagTypes = tagTypes;
            SelectedTypeIndex = TagTypes.IndexOf(TagTypes.Single(it => it.ObjectId == tag.TypeId));
            DisplayAliases();
            ExplorerHeader = new ExplorerHeader()
            {
                CloseButtonEnabled = true,
                Header = Name
            };
        }

        public TagDetailViewModel()
        {
            Editable = true;
            SelectedTypeIndex = -1;
            DisplayAliases();
            Tag = new Tag()
            {
                ObjectId = -1
            };
        }
    }
}
