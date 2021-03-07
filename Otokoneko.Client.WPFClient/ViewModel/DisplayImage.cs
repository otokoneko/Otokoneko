using System.Windows;
using System.Windows.Media.Imaging;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public class DisplayImage : BaseViewModel
    {
        private bool _loaded = false;

        private BitmapSource _realSource;
        private Int32Rect? _cropRect;

        public Image Image { get; set; }

        public Int32Rect? CropRect
        {
            get => _cropRect;
            set
            {
                _cropRect = value;
                OnPropertyChanged(nameof(Source));
            }
        }

        public BitmapSource RealSource
        {
            get => _realSource;
            set
            {
                _realSource = value;
                if (value == null) _loaded = false;
                OnPropertyChanged(nameof(Source));
            }
        }

        public BitmapSource Source
        {
            get
            {
                if (!_loaded)
                    LoadImage();
                _loaded = true;
                if (CropRect == null || RealSource == null)
                {
                    return RealSource;
                }

                var source = new CroppedBitmap(RealSource, (Int32Rect)CropRect);
                source.Freeze();
                return source;
            }
        }

        public double Height { get; set; }

        public double Width { get; set; }

        public double ActualWidth { get; set; }
        public double ActualHeight { get; set; }

        private async void LoadImage()
        {
            var imageContent = await Model.GetImage(Image.ObjectId);
            RealSource = WPFClient.Model.Utils.Convert(imageContent);
        }

        public DisplayImage(Image image, double width, double height, Int32Rect? cropRect)
        {
            Image = image;
            Width = width;
            Height = height;
            _cropRect = cropRect;
        }
    }
}
