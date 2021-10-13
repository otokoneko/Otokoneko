using System.Windows;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// PlanManager.xaml 的交互逻辑
    /// </summary>
    public partial class PlanManager
    {
        public PlanManager()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if(DataContext == null) return;
            await ((dynamic) DataContext).OnLoaded();
        }
    }
}
