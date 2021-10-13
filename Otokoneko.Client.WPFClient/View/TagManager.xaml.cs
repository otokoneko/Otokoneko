using System.Windows;
using System.Windows.Controls;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// TagManager.xaml 的交互逻辑
    /// </summary>
    public partial class TagManager
    {
        public TagManager()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not null)
            {
                await ((dynamic)DataContext).OnLoaded();
            }
        }
        
        // private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        // {
        //     if (e.AddedItems.Count > 0 &&
        //         Explorer.SelectedIndex == Explorer.Items.Count - 1 && 
        //         !_dragging)
        //     {
        //         Explorer.SelectedIndex = Explorer.Items.Count - 2;
        //     }
        // }

        private async void ScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalOffset + e.ViewportHeight == e.ExtentHeight && DataContext != null)
            {
                await ((dynamic) DataContext).LoadTags();
            }
        }

        // private void Selector_OnSelected(object sender, RoutedEventArgs e)
        // {
        // }

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext == null) return;
            var comboBox = sender as ComboBox;
            ((dynamic)DataContext).SelectedTagTypeIndex = comboBox.SelectedIndex;
            if (comboBox.SelectedIndex == comboBox.Items.Count - 1)
            {
                comboBox.SelectedItem = e.RemovedItems.Count != 0 ? e.RemovedItems[0] : null;
            }
        }
    }
}
