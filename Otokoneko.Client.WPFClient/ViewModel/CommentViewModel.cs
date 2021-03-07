using Otokoneko.DataType;

namespace Otokoneko.Client.WPFClient.ViewModel
{
    public static partial class Constant
    {
        public const string CurrentScoreTemplate = "当前评分：{0}/10";
    }

    public class CommentViewModel : BaseViewModel
    {
        private readonly Comment _comment;
        public string ScoreText { get; set; }
        public string CommentText { get; set; }
        public int Score { get; set; }

        public Comment Comment
        {
            get
            {
                _comment.Score = Score;
                _comment.Text = CommentText;
                return _comment;
            }
        }

        public CommentViewModel(Comment comment)
        {
            _comment = comment;
            CommentText = _comment.Text;
            ChangeScore(comment.Score);
            OnPropertyChanged(nameof(CommentText));
        }

        public void ChangeScore(int score)
        {
            Score = score;
            ScoreText = string.Format(Constant.CurrentScoreTemplate, Score);
            OnPropertyChanged(nameof(Score));
            OnPropertyChanged(nameof(ScoreText));
        }
    }
}
