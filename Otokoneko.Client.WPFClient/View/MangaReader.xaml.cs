using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Otokoneko.Client.WPFClient.ViewModel;
using Otokoneko.DataType;
using Image = System.Windows.Controls.Image;

namespace Otokoneko.Client.WPFClient.View
{
    public class ImageListBox : ListBox
    {
        public ImageListBox()
        {
            SelectionChanged += ScrollToSelectedItem;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var border = (Border)VisualTreeHelper.GetChild(this, 0);
            var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
            scrollViewer.ScrollChanged += SetProgress;
        }

        private void SetProgress(object sender, ScrollChangedEventArgs e)
        {
            if (!IsLoaded) return;
            var point = new Point(ActualWidth / 2, ActualHeight / 2);
            for (var i = 0; i < Items.Count; i++)
            {
                var lbi = ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (lbi == null) continue;
                var bounds = VisualTreeHelper.GetDescendantBounds(lbi);
                if (!bounds.Contains(point)) continue;
                ((dynamic)DataContext).SetProgress(i + 1);
                break;
            }
        }

        private void ScrollToSelectedItem(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedItem != null)
            {
                ScrollIntoView(SelectedItem);
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
            await ((dynamic) DataContext).OnClosed();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            ((dynamic) DataContext).GetWidth = new Func<double>(() => ActualWidth);
            ((dynamic) DataContext).GetHeight = new Func<double>(() => ActualHeight);
            ((dynamic) DataContext).GetTitleBarHeight = new Func<double>(() => TitleBarHeight);
            await ((dynamic) DataContext).OnLoaded();
        }

        private void ZoomImages(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            e.Handled = true;
            ((UIElement)sender).RaiseEvent(e);
            ((dynamic) DataContext).Zoom(e.Delta);
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
            else
            {
                if (_hiding)
                    ShowExplorerToolBar();
            }
        }

        private void PageSliderOnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            ((dynamic) DataContext).ScrollTo(((int) Slider.Value) - 1);
        }

        private void UIElement_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ((dynamic) sender).DataContext.ChangePage(e.Delta / 120);
        }
    }
}