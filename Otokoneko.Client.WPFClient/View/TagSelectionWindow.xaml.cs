using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Otokoneko.Client.WPFClient.ViewModel;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// TagSelectionWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TagSelectionWindow
    {
        public List<Tag> SelectedTags { get; set; }

        public TagSelectionWindow(long typeId, List<Tag> selectedTags)
        {
            InitializeComponent();
            DataContext = new TagSelectionViewModel(typeId, selectedTags);
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ((dynamic) DataContext).OnLoaded();
        }

        private void Confirm_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            SelectedTags = ((dynamic)DataContext).GetResult();
            Close();
        }


        private void Cancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void ScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalOffset + e.ViewportHeight == e.ExtentHeight && DataContext != null)
            {
                await((dynamic)DataContext).LoadTags();
            }
        }
    }
}
