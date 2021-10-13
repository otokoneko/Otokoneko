using System.Windows;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// TaskDetailView.xaml 的交互逻辑
    /// </summary>
    public partial class TaskDetailView
    {
        public TaskDetailView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext == null) return;
            await ((dynamic)DataContext).OnLoaded();
        }
    }
}
