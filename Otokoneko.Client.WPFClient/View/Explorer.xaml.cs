using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.Controls;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// Explorer.xaml 的交互逻辑
    /// </summary>
    public partial class Explorer
    {
        public Explorer()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext == null) return;
            Loaded -= OnLoaded;
            await ((dynamic) DataContext).OnLoaded();
        }

        private void PreviewMouseMoveHandler(object sender, MouseEventArgs e)
        {
            if (!(e.Source is MetroTabItem tabItem))
            {
                return;
            }

            if (Mouse.PrimaryDevice.LeftButton == MouseButtonState.Pressed &&
                _validDragItem == tabItem &&
                (e.GetPosition(this) - _dragStartPosition).Length > 10)
            {
                DragDrop.DoDragDrop(tabItem, tabItem, DragDropEffects.All);
            }
        }

        private void DropHandler(object sender, DragEventArgs e)
        {
            var tabItemTarget = e.Source as MetroTabItem;
            var tabItemSource = e.Data.GetData(typeof(MetroTabItem)) as MetroTabItem;

            var source = tabItemSource?.DataContext;
            var target = tabItemTarget?.DataContext;

            _dragging = true;
            ((dynamic)DataContext).Move(source, target);
            _dragging = false;

            TabControl.SelectedItem = source;
        }

        private MetroTabItem _validDragItem;
        private MetroTabItem _mouseDownItem;
        private Point _dragStartPosition;
        private bool _dragging;

        private void PreviewMouseLeftButtonDownHandler(object sender, MouseButtonEventArgs e)
        {
            if (!(e.Source is MetroTabItem tabItem))
            {
                e.Handled = false;
                _validDragItem = null;
                return;
            }

            _mouseDownItem = tabItem;
            _dragStartPosition = e.GetPosition(this);
            _validDragItem =
                ((dynamic)DataContext).IsDraggable(tabItem.DataContext)
                    ? tabItem
                    : null;
        }

        private void MouseLeftButtonUpHandler(object sender, MouseButtonEventArgs e)
        {
            if (!(e.Source is MetroTabItem tabItem)) return;
            if (((dynamic)DataContext).CouldCreateNewTab && tabItem == _mouseDownItem && TabControl.Items.IndexOf(tabItem.DataContext) == TabControl.Items.Count - 1)
            {
                ((dynamic)DataContext).CreateNewTab();
            }
        }

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateLayout();
            if (DataContext != null && ((dynamic) DataContext).CouldCreateNewTab == false) return;
            if (e.AddedItems.Count > 0 &&
                TabControl.SelectedIndex == TabControl.Items.Count - 1 &&
                !_dragging)
            {
                TabControl.SelectedIndex = TabControl.Items.Count - 2;
            }
        }

    }
}
