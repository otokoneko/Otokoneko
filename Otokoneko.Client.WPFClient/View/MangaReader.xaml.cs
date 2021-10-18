using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Otokoneko.Client.WPFClient.Utils;
using Otokoneko.Client.WPFClient.ViewModel;
using Otokoneko.DataType;
using Image = System.Windows.Controls.Image;

namespace Otokoneko.Client.WPFClient.View
{
    public class ImageListBox : ListBox
    {
        private double target;
        private const double EPS = 1;

        public ImageListBox()
        {
            IsSynchronizedWithCurrentItem = true;
            SelectionChanged += (s, e) => ScrollToSelectedItem();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var scrollViewer = this.GetChild<ScrollViewer>();
            scrollViewer.ScrollChanged += SetProgress;
        }

        private void SetProgress(object sender, ScrollChangedEventArgs e)
        {
            if (!IsLoaded) return;

            if (Math.Abs(target - e.VerticalOffset) < EPS)
            {
                SelectedItem = null;
            }
            else if (e.ExtentHeightChange != 0 && SelectedItem != null)
            {
                ScrollToSelectedItem();
            }

            var hasSetProgress = false;
            var offset = (sender as ScrollViewer).VerticalOffset;
            var screenUp = offset / ((dynamic)DataContext).ScaleValue;
            var screenDown = (offset + ActualHeight) / ((dynamic)DataContext).ScaleValue;
            var height = .0;
            int left = int.MaxValue, right = int.MinValue;
            for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                var image = item as DisplayImage;
                var imageUp = height;
                var imageDown = height + image.ActualHeight;
                if (imageDown > screenUp && imageUp < screenDown)
                {
                    left = Math.Min(left, i);
                    right = Math.Max(right, i);
                    if (!hasSetProgress)
                    {
                        hasSetProgress = true;
                        ((dynamic)DataContext).SetProgress(i + 1);
                    }
                }
                height = imageDown;
            }

            for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                var image = item as DisplayImage;
                image.Readable = i >= left - 1 && i <= right + 2;
            }
        }

        private void ScrollToSelectedItem()
        {
            if (SelectedItem == null) return;

            var height = .0;
            for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                var image = item as DisplayImage;
                if (image == SelectedItem) break;
                height += image.ActualHeight;
            }
            height *= ((dynamic)DataContext).ScaleValue;
            
            if (height == target) return;

            if (SelectedItem != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateLayout();
                    var sc = this.GetChild<ScrollViewer>();
                    target = height;
                    sc.ScrollToVerticalOffset(height);
                    SelectedItem = null;
                }));
            }
        }
    }

    /// <summary>
    /// MangaReader.xaml 的交互逻辑
    /// </summary>
    public partial class MangaReader
    {
        public MangaReader(List<Chapter> chapters, int index)
        {
            InitializeComponent();
            DataContext = new MangaReaderViewModel(chapters, index);
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private async void OnClosed(object? sender, EventArgs e)
        {
            await ((dynamic)DataContext).OnClosed();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            ((dynamic)DataContext).GetWidth = new Func<double>(() => ActualWidth);
            ((dynamic)DataContext).GetHeight = new Func<double>(() => ActualHeight);
            ((dynamic)DataContext).GetTitleBarHeight = new Func<double>(() => TitleBarHeight);
            await ((dynamic)DataContext).OnLoaded();
        }

        private void ZoomImages(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            e.Handled = true;
            ((UIElement)sender).RaiseEvent(e);
            ((dynamic)DataContext).Zoom(e.Delta);
        }

        private void ImageOnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var image = sender as Image;
            if (!(image.DataContext is DisplayImage i)) return;
            if ((bool)e.NewValue == false)
            {
                i.RealSource = null;
            }
        }

        private bool _hiding = false;

        private void HideExplorerToolBar()
        {
            _hiding = true;
            var margin = ExplorerToolBar.Margin;
            ExplorerToolBar.BeginAnimation(MarginProperty, new ThicknessAnimation(
                margin,
                new Thickness(margin.Left, margin.Top, margin.Right, -(10 + ExplorerToolBar.ActualHeight)),
                new Duration(TimeSpan.FromMilliseconds(300))));
        }

        private void ShowExplorerToolBar()
        {
            _hiding = false;
            var margin = ExplorerToolBar.Margin;
            ExplorerToolBar.BeginAnimation(MarginProperty, new ThicknessAnimation(
                margin,
                new Thickness(margin.Left, margin.Top, margin.Right, 30),
                new Duration(TimeSpan.FromMilliseconds(300))));
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.GetPosition(this).Y < ActualHeight - ExplorerToolBar.ActualHeight - 100)
            {
                if (!_hiding)
                    HideExplorerToolBar();
            }
            else if (e.GetPosition(this).Y >= ActualHeight * 0.8)
            {
                if (_hiding)
                    ShowExplorerToolBar();
            }
        }

        private void PageSliderOnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            ((dynamic)DataContext).ScrollTo(((int)Slider.Value) - 1);
        }

        private void UIElement_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta < 0)
            {
                ((dynamic)DataContext).NextImageButton.Command.ExecuteAsync();
            }
            else
            {
                ((dynamic)DataContext).PrevImageButton.Command.ExecuteAsync();
            }
        }
    }
}