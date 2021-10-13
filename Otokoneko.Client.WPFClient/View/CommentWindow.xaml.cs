using System.Windows;
using System.Windows.Input;
using Otokoneko.Client.WPFClient.ViewModel;
using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// CommentWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CommentWindow
    {
        public Comment Comment => ((dynamic) DataContext).Comment;

        public CommentWindow(Comment comment)
        {
            InitializeComponent();
            DataContext = new CommentViewModel(comment);
        }

        private void ScoreControlOnMouseMove(object sender, MouseEventArgs e)
        {
            var x = e.GetPosition(ScoreControl).X;
            var score = (int)(10 * (x / (ScoreControl.ActualWidth)) + 0.5);
            ((dynamic) DataContext).ChangeScore(score);
        }

        private void ConfirmButtonOnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }


        private void CancelButtonOnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
