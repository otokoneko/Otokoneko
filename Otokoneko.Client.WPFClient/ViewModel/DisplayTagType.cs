using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AsyncAwaitBestPractices.MVVM;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public static partial class Constant
    {
        public const string DeleteTagTypeTemplate = "该操作将会删除选中的标签类型 {0} 以及该类型所拥有的所有标签，是否继续？";

        public const string AddTagTypeSuccess = "添加标签类型成功";
        public const string AddTagTypeFail = "添加标签类型失败，请检查是否存在重复的标签类型";
    }

    class DisplayTagType : BaseViewModel
    {
        private TagType TagType { get; set; }

        private Color _color;
        public Color Color
        {
            get => _color;
            set
            {
                _color = value;
                ChangeButtonEnable();
            }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                ChangeButtonEnable();
            }
        }

        public bool Editable { get; set; }

        private void ChangeButtonEnable()
        {
            CheckButton.IsEnable = _color != TagType.Color || _name != TagType.Name;
            DeleteButton.IsEnable = !CheckButton.IsEnable;
            OnPropertyChanged(nameof(CheckButton));
            OnPropertyChanged(nameof(DeleteButton));
        }

        public CircleButtonViewModel CheckButton { get; set; }

        public CircleButtonViewModel DeleteButton { get; set; }

        public ICommand DeleteCommand => new AsyncCommand(async () =>
        {
            var result =
                MessageBox.Show(string.Format(Constant.DeleteTagTypeTemplate, TagType.Name), Constant.OperateNotice, MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;
            var success = await Model.DeleteTagType(TagType.ObjectId);
            MessageBox.Show(success ? Constant.DeleteSuccess: Constant.DeleteFail);
            await Model.ListTagTypes();
        });

        public ICommand CheckCommand => new AsyncCommand(async () =>
        {
            if (TagType.ObjectId > 0)
            {
                TagType.Color = Color;
                ChangeButtonEnable();
            }
            else
            {
                var objectId = await Model.AddTagType(Name);
                if (objectId > 0)
                {
                    TagType.ObjectId = objectId;
                    TagType.Color = Color;
                    MessageBox.Show(Constant.AddTagTypeSuccess);
                }
                else
                {
                    MessageBox.Show(Constant.AddTagTypeFail);
                }
                await Model.ListTagTypes();
            }
        });

        public DisplayTagType(TagType tagType)
        {
            CheckButton = new CircleButtonViewModel()
            {
                Image = "./icon/check.png",
                IsEnable = false,
                Command = CheckCommand
            };
            DeleteButton = new CircleButtonViewModel()
            {
                Image = "./icon/close.png",
                IsEnable = true,
                Command = DeleteCommand
            };

            TagType = tagType;
            Color = tagType.Color;
            Name = tagType.Name;

            Editable = TagType.ObjectId <= 0;
        }
    }
}
