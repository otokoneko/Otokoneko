using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.Controls;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// InputForm.xaml 的交互逻辑
    /// </summary>
    public partial class InputForm : MetroWindow
    {
        public string InputMessage { get; set; }
        private readonly Func<string, bool> _filter;

        public InputForm(string title, string message, string inputMessage, Func<string, bool> filter = null)
        {
            InitializeComponent();
            _filter = filter;
            Title = title;
            Label.Text = message;
            InputBox.Text = inputMessage;
            InputBox.KeyUp += InputBox_KeyUp;
            Loaded += InputForm_Loaded;
        }

        private void InputBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                InputMessage = InputBox.Text;
                DialogResult = true;
                Close();
            }
        }

        private void InputForm_Loaded(object sender, RoutedEventArgs e)
        {
            InputBox.SelectAll();
            Keyboard.Focus(InputBox);
        }

        private void Commit(object sender, RoutedEventArgs e)
        {
            InputMessage = InputBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void Cancle(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_filter != null && !_filter(InputBox.Text))
            {
                InputBox.Text = "";
            }
        }
    }

}
