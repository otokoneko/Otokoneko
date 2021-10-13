using System.Windows;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// TaskScheduler.xaml 的交互逻辑
    /// </summary>
    public partial class TaskScheduler
    {
        public TaskScheduler()
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
