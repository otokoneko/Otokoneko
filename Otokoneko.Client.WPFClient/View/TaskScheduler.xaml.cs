using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
