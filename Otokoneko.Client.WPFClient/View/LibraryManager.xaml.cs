using System.Windows;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// LibraryManager.xaml 的交互逻辑
    /// </summary>
    public partial class LibraryManager
    {
        public LibraryManager()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if(DataContext == null) return;
            Loaded -= OnLoaded;
            await ((dynamic) DataContext).OnLoaded();
        }
    }
}
