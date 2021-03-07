using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if(DataContext == null) return;
            ((dynamic)DataContext).CloseWindow = new Action(Close);
            await ((dynamic) DataContext).OnLoaded();
        }

        private void MainWindow_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled)
            {
                e.Handled = false;
                return;
            }
            Keyboard.ClearFocus();
            e.Handled = false;
        }
    }
}
