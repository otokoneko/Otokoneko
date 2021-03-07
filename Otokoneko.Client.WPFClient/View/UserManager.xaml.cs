using System.Windows;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// UserManager.xaml 的交互逻辑
    /// </summary>
    public partial class UserManager
    {
        public UserManager()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ((dynamic) DataContext).OnLoaded();
        }
    }
}
