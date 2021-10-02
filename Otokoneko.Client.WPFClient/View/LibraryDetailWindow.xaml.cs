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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Otokoneko.Client.WPFClient.ViewModel;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// LibraryDetailWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LibraryDetailWindow
    {
        public LibraryDetailWindow(long libraryId)
        {
            InitializeComponent();
            DataContext = new LibraryDetailViewModel(libraryId, this);
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            await ((dynamic) DataContext).OnLoaded();
        }
    }
}
