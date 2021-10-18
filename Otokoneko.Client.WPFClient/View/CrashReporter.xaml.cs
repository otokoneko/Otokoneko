using System;
using System.Windows;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// CrashReporter.xaml 的交互逻辑
    /// </summary>
    public partial class CrashReporter : Window
    {
        public CrashReporter(string title, Exception exception)
        {
            InitializeComponent();
            Title = title;
            Report.Text = exception.ToString();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Report.SelectAll();
            Report.Focus();
            Clipboard.SetDataObject(Report.SelectedText);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
