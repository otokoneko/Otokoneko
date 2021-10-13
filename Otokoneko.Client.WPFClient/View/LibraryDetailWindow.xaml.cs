using System.Windows;
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
