using System;
using System.Threading;
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
        private bool _readable;
        public bool Readable
        {
            get => _readable;
            set
            {
                if (_readable == value) return;
                _readable = value;
                if(!_readable)
                {
                    RealSource = null;
                }
                else
                {
                    OnPropertyChanged(nameof(Source));
                }
            }
        }

        private int _loaded = 0;

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
                if (value == null) _loaded = 0;
                else
                {
                    var newHeight = .0;
                    var newWidth = .0;
                    switch (ImageResizeMode)
                    {
                        case ImageResizeMode.RespectWidth:
                            newWidth = ExpectedWidth;
                            if(RealSource.PixelHeight <= RealSource.PixelWidth && AutoCropMode != AutoCropMode.None)
                            {
                                var width = (RealSource.PixelWidth + 1) / 2;
                                newHeight = (double)RealSource.PixelHeight / width * newWidth;
                            }
                            else
                            {
                                newHeight = (double)RealSource.PixelHeight / RealSource.PixelWidth * newWidth;
                                newHeight = AutoCropMode == AutoCropMode.LeftToRight
                                ? (ImageCropMode == ImageCropMode.Left ? newHeight : 0)
                                : (ImageCropMode == ImageCropMode.Right ? newHeight : 0);
                            }
                            break;
                        case ImageResizeMode.RespectHeight:
                            newHeight = ExpectedHeight;
                            newWidth = (double)RealSource.PixelWidth / RealSource.PixelHeight * newHeight;
                            break;
                    }

                    Visable = (RealSource.PixelHeight <= RealSource.PixelWidth && AutoCropMode != AutoCropMode.None) ||
                        (AutoCropMode == AutoCropMode.LeftToRight && ImageCropMode == ImageCropMode.Left) ||
                        (AutoCropMode != AutoCropMode.LeftToRight && ImageCropMode == ImageCropMode.Right);

                    if(ActualHeight != newHeight)
                    {
                        ActualHeight = newHeight;
                        OnPropertyChanged(nameof(ActualHeight));
                    }
                    if(ActualWidth != newWidth)
                    {
                        ActualWidth = newWidth;
                        OnPropertyChanged(nameof(ActualWidth));
                    }
                    OnPropertyChanged(nameof(Visable));
                }
                OnPropertyChanged(nameof(Source));
            }
        }

        public BitmapSource Source
        {
            get
            {
                if (!Readable) 
                    return null;

                if (Interlocked.Increment(ref _loaded) == 1)
                {
                    LoadImage();
                }

                if (RealSource == null) return null;

                BitmapSource source;

                if (RealSource.PixelHeight > RealSource.PixelWidth || AutoCropMode == AutoCropMode.None)
                {
                    source = AutoCropMode == AutoCropMode.LeftToRight
                        ? (ImageCropMode == ImageCropMode.Left ? RealSource : null)
                        : (ImageCropMode == ImageCropMode.Right ? RealSource : null);
                }
                else
                {
                    var width = (RealSource.PixelWidth + 1) / 2;
                    source = ImageCropMode switch
                    {
                        ImageCropMode.Right => new CroppedBitmap(RealSource, new Int32Rect(RealSource.PixelWidth - width, 0, width, RealSource.PixelHeight)),
                        ImageCropMode.Left => new CroppedBitmap(RealSource, new Int32Rect(0, 0, width, RealSource.PixelHeight)),
                        _ => throw new ArgumentOutOfRangeException(),
                    };

                    source.Freeze();
                    return source;
                }

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
