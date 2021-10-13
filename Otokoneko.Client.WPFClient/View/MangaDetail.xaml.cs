using System.Windows;
using System.Windows.Controls;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// MangaDetail.xaml 的交互逻辑
    /// </summary>
    public partial class MangaDetail
    {
        public MangaDetail()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ((dynamic) DataContext).OnLoaded();
        }

        private void ChapterList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && DataContext != null)
            {
                ((dynamic) DataContext).SelectedChapterIndex = listBox.SelectedIndex;
            }
        }

        private object _draggedItem;

        // private P FindVisualParent<P>(DependencyObject child) where P : DependencyObject
        // {
        //     var parentObject = VisualTreeHelper.GetParent(child);
        //     return parentObject switch
        //     {
        //         null => null,
        //         P parent => parent,
        //         _ => FindVisualParent<P>(parentObject)
        //     };
        // }
        //
        // private void ChapterItemDragRectangleOnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        // {
        //     _draggedItem = ((dynamic)sender).DataContext;
        // }
        //
        // private void DragChapterItemOnPreviewMouseMove(object sender, MouseEventArgs e)
        // {
        //     if (!(DataContext is MangaDetailViewModel viewModel)) return;
        //     if (_draggedItem == null) return;
        //     if (e.LeftButton != MouseButtonState.Pressed)
        //     {
        //         _draggedItem = null;
        //         return;
        //     }
        //     var dst = ((dynamic)sender).DataContext as Chapter;
        //     viewModel.Move(_draggedItem, dst);
        // }
        //
        // private void ChapterItemDragRectangleOnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        // {
        //     _draggedItem = null;
        // }
    }
}
