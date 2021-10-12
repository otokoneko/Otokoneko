using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using Otokoneko.Client.WPFClient.Utils;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public enum ImageCropMode
    {
        None,
        Right,
        Left
    }

    public enum ImageResizeMode
    {
        RespectWidth,
        RespectHeight,
    }

    public class DisplayImage : BaseViewModel
    {
        private bool _loaded = false;

        private BitmapSource _realSource;

        public Image Image { get; set; }

        private AutoCropMode CropMode { get; set; }

        private ImageResizeMode _imageResizeMode;
        public ImageResizeMode ImageResizeMode
        {
            get => _imageResizeMode;
            set
            {
                _imageResizeMode = value;
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
                else
                {
                    switch (ImageResizeMode)
                    {
                        case ImageResizeMode.RespectWidth:
                            ActualWidth = ExpectedWidth;
                            if(RealSource.PixelHeight <= RealSource.PixelWidth && CropMode != AutoCropMode.None)
                            {
                                var width = (RealSource.PixelWidth + 1) / 2;
                                ActualHeight = (double)RealSource.PixelHeight * 2 / width * ActualWidth;
                            }
                            else
                            {
                                ActualHeight = (double)RealSource.PixelHeight / RealSource.PixelWidth * ActualWidth;
                            }
                            break;
                        case ImageResizeMode.RespectHeight:
                            ActualHeight = ExpectedHeight;
                            ActualWidth = (double)RealSource.PixelWidth / RealSource.PixelHeight * ActualHeight;
                            break;

                    }
                    OnPropertyChanged(nameof(ActualWidth));
                    OnPropertyChanged(nameof(ActualHeight));
                }
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
                if (RealSource == null || 
                    CropMode == AutoCropMode.None || 
                    ImageResizeMode == ImageResizeMode.RespectHeight || 
                    RealSource.PixelHeight > RealSource.PixelWidth)
                {
                    return RealSource;
                }
                var width = (RealSource.PixelWidth + 1) / 2;
                var right = new CroppedBitmap(RealSource, new Int32Rect(RealSource.PixelWidth - width, 0, width, RealSource.PixelHeight));
                var left = new CroppedBitmap(RealSource, new Int32Rect(0, 0, width, RealSource.PixelHeight));
                var source = CropMode switch
                {
                    AutoCropMode.RightToLeft => ImageUtils.Merge(right, left),
                    AutoCropMode.LeftToRight => ImageUtils.Merge(left, right),
                };

                source.Freeze();
                return source;
            }
        }

        public double ExpectedWidth { get; set; }
        public double ExpectedHeight { get; set; }
        public double ActualWidth { get; set; }
        public double ActualHeight { get; set; }

        private async void LoadImage()
        {
            var imageContent = await Model.GetImage(Image.ObjectId);
            RealSource = ImageUtils.Convert(imageContent);
        }

        public DisplayImage(Image image, double expectedWidth, double expectedHeight, AutoCropMode imageCropMode, ImageResizeMode imageResizeMode)
        {
            Image = image;
            ActualWidth = ExpectedWidth = expectedWidth;
            ActualHeight = ExpectedHeight = expectedHeight;
            CropMode = imageCropMode;
            ImageResizeMode = imageResizeMode;
        }
    }
}
