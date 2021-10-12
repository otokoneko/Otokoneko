using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// MangaSearchResult.xaml 的交互逻辑
    /// </summary>
    public partial class MangaSearchResult
    {
        private double _continuousUpOffset = 0;
        private const double UpThreshold = 48;
        private double _continuousDownOffset = 0;
        private const double DownThreshold = 48 * 3;
        private bool _hiding = false;

        public MangaSearchResult()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public static ScrollViewer GetScrollViewer(DependencyObject o)
        {
            if (o is ScrollViewer s) return s;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var child = VisualTreeHelper.GetChild(o, i);
                var result = GetScrollViewer(child);
                if (result == null) continue;
                return result;
            }
            return null;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            var scrollViewer = GetScrollViewer(MangaListBox);
            await ((dynamic)DataContext).OnLoaded();
            scrollViewer.ScrollToVerticalOffset(((dynamic) DataContext).InitVerticalOffset);
            ((dynamic) DataContext).ScrollToTop = new Action(() => scrollViewer.ScrollToTop());
        }

        private void HidePagination()
        {
           var margin = Pagination.Margin;
           Pagination.BeginAnimation(MarginProperty, new ThicknessAnimation(
               margin,
               new Thickness(margin.Left, margin.Top, margin.Right, -(Pagination.ActualHeight + 10)),
               new Duration(TimeSpan.FromMilliseconds(300))));
        }

        private void ShowPagination()
        {
            var margin = Pagination.Margin;
            Pagination.BeginAnimation(MarginProperty, new ThicknessAnimation(
                margin,
                new Thickness(margin.Left, margin.Top, margin.Right, 50),
                new Duration(TimeSpan.FromMilliseconds(300))));
        }
        
        private void ScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!IsLoaded) return;
            // scroll down
            if (e.VerticalChange > 0)
            {
                _continuousUpOffset = 0;
                _continuousDownOffset += e.VerticalChange;
                if ((int)Math.Abs(_continuousDownOffset) >= (int)DownThreshold && !_hiding)
                {
                    _hiding = true;
                    HidePagination();
                }

                if (e.VerticalOffset + e.ViewportHeight == e.ExtentHeight && _hiding)
                {
                    _hiding = false;
                    ShowPagination();
                }
            }
            // scroll up
            else if (e.VerticalChange < 0)
            {
                _continuousDownOffset = 0;
                _continuousUpOffset += e.VerticalChange;
                if ((int)Math.Abs(_continuousUpOffset) >= (int)UpThreshold && _hiding)
                {
                    _hiding = false;
                    ShowPagination();
                }
            }

            ((dynamic)DataContext).VerticalOffset = e.VerticalOffset;
        }
        
        private void MangaListBox_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Grid.Focus();
            e.Handled = false;
        }
    }
}
