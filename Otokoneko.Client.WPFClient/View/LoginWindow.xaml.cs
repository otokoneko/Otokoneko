using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.Controls;
using Otokoneko.Client.WPFClient.ViewModel;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// LoginWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LoginWindow : MetroWindow
    {
        public LoginWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LoginViewModel viewModel) return;
            viewModel.CloseWindow = Close;
            await viewModel.OnLoaded();
        }
    }
}
