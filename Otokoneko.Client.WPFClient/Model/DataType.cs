using System.Windows.Media;
using MessagePack;

namespace Otokoneko.DataType
{
    public partial class TagType
    {
        private static Client.Model Model { get; set; } = Client.Model.Instance;

        private Color? _color;

        [IgnoreMember]
        public Color Color
        {
            get => _color ??= Model.GetColor(ObjectId);
            set
            {
                _color = value;
                Model.SetColor(ObjectId, (Color)_color);
            }
        }
    }
}