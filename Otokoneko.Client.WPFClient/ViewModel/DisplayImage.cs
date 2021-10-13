using System;
using System.Windows;
using System.Windows.Media.Imaging;
using Otokoneko.Client.WPFClient.Utils;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public enum ImageCropMode
    {
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

        private ImageCropMode ImageCropMode { get; set; }
        private AutoCropMode AutoCropMode { get; set; }

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
                            if(RealSource.PixelHeight <= RealSource.PixelWidth && AutoCropMode != AutoCropMode.None)
                            {
                                var width = (RealSource.PixelWidth + 1) / 2;
                                ActualHeight = (double)RealSource.PixelHeight / width * ActualWidth;
                            }
                            else
                            {
                                ActualHeight = (double)RealSource.PixelHeight / RealSource.PixelWidth * ActualWidth;
                                ActualHeight = AutoCropMode == AutoCropMode.LeftToRight
                                ? (ImageCropMode == ImageCropMode.Left ? ActualHeight : 0)
                                : (ImageCropMode == ImageCropMode.Right ? ActualHeight : 0);
                            }
                            break;
                        case ImageResizeMode.RespectHeight:
                            ActualHeight = ExpectedHeight;
                            ActualWidth = (double)RealSource.PixelWidth / RealSource.PixelHeight * ActualHeight;
                            break;
                    }

                    Visable = (RealSource.PixelHeight <= RealSource.PixelWidth && AutoCropMode != AutoCropMode.None) ||
                        (AutoCropMode == AutoCropMode.LeftToRight && ImageCropMode == ImageCropMode.Left) ||
                        (AutoCropMode != AutoCropMode.LeftToRight && ImageCropMode == ImageCropMode.Right);

                    OnPropertyChanged(nameof(ActualWidth));
                    OnPropertyChanged(nameof(ActualHeight));
                    OnPropertyChanged(nameof(Visable));
                }
                OnPropertyChanged(nameof(Source));
            }
        }

        public BitmapSource Source
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    LoadImage();
                }

                if (RealSource == null)
                {
                    return null;
                }

                if(RealSource.PixelHeight > RealSource.PixelWidth || AutoCropMode == AutoCropMode.None)
                {
                    return AutoCropMode == AutoCropMode.LeftToRight
                        ? (ImageCropMode == ImageCropMode.Left ? RealSource : null)
                        : (ImageCropMode == ImageCropMode.Right ? RealSource : null);
                }

                var width = (RealSource.PixelWidth + 1) / 2;
                var source = ImageCropMode switch
                {
                    ImageCropMode.Right => new CroppedBitmap(RealSource, new Int32Rect(RealSource.PixelWidth - width, 0, width, RealSource.PixelHeight)),
                    ImageCropMode.Left => new CroppedBitmap(RealSource, new Int32Rect(0, 0, width, RealSource.PixelHeight)),
                    _ => throw new ArgumentOutOfRangeException(),
                };

                source.Freeze();
                return source;
            }
        }

        public double ExpectedWidth { get; set; }
        public double ExpectedHeight { get; set; }
        public double ActualWidth { get; set; }
        public double ActualHeight { get; set; }
        public bool Visable { get; set; }

        private async void LoadImage()
        {
            var imageContent = await Model.GetImage(Image.ObjectId);
            RealSource = ImageUtils.Convert(imageContent);
        }

        public DisplayImage(Image image, bool visable, double expectedWidth, double expectedHeight, AutoCropMode autoCropMode, ImageCropMode imageCropMode, ImageResizeMode imageResizeMode)
        {
            Image = image;
            Visable = visable;
            ActualWidth = ExpectedWidth = expectedWidth;
            ActualHeight = ExpectedHeight = expectedHeight;
            AutoCropMode = autoCropMode;
            ImageCropMode = imageCropMode;
            ImageResizeMode = imageResizeMode;
        }
    }
}
