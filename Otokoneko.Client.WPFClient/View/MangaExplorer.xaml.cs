using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using Otokoneko.Client.WPFClient.ViewModel;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// MangaExplorer.xaml 的交互逻辑
    /// </summary>
    public partial class MangaExplorer
    {
        public MangaExplorer()
        {
            InitializeComponent();
        }

        private void UIElement_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (sender is Border item)
            {
                ((DisplayTag)item.DataContext).ClickCommand?.Execute(null);
            }

            if (sender is Rectangle rectangle)
            {
                ((dynamic)DataContext).RemoveSearchHistory((string)rectangle.DataContext);
            }

            if (sender is TextBlock textBox)
            {
                ((dynamic)DataContext).SetKeyword(textBox.Text);
            }
        }

        private void UIElement_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }
}
