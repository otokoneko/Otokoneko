using System.Windows.Input;
using System.Windows.Media;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public class DisplayTag: BaseViewModel
    {
        public long ObjectId { get; set; }
        public long Key { get; set; }
        public string Name { get; set; }
        public long TypeId { get; set; }
        public Color Color { get; set; }

        public ICommand ClickCommand { get; set; }

        public DisplayTag(Tag tag)
        {
            ObjectId = tag.ObjectId;
            Key = tag.Key;
            Name = tag.Name;
            TypeId = tag.TypeId;
            Color = Model.GetColor(TypeId);
        }

        public DisplayTag()
        {

        }
    }

}
