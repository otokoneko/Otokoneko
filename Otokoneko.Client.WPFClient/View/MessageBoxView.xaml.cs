using System.Windows;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// MessageBoxView.xaml 的交互逻辑
    /// </summary>
    public partial class MessageBoxView
    {
        public MessageBoxView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private object _dataContext;

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            _dataContext = DataContext;
            await ((dynamic)DataContext).OnLoaded();
        }

        private bool _checked = false;

        private async void UIElement_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_checked) return;
            _checked = true;
            await ((dynamic)_dataContext).Check();
        }
    }
}
