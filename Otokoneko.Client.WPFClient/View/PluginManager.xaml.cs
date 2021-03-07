using System.Windows;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// PluginManager.xaml 的交互逻辑
    /// </summary>
    public partial class PluginManager
    {
        public PluginManager()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext == null) return;
            await ((dynamic) DataContext).OnLoaded();
        }
    }
}
