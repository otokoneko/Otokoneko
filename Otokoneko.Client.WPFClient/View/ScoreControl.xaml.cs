using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Otokoneko.Client.WPFClient.View
{
    /// <summary>
    /// ScoreControl.xaml 的交互逻辑
    /// </summary>
    public partial class ScoreControl : UserControl
    {
        public static readonly DependencyProperty ScoreProperty =
            DependencyProperty.Register(nameof(Score), typeof(int), typeof(ScoreControl), new FrameworkPropertyMetadata()
            {
                DefaultValue = 0,
                BindsTwoWayByDefault = true,
                PropertyChangedCallback = ScoreChanged,
                DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        private static void ScoreChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ScoreControl)d;
            control.ChangeStarsByScore();
        }

        public int Score
        {
            get => (int)GetValue(ScoreProperty);
            set
            {
                SetValue(ScoreProperty, value);
                ChangeStarsByScore();
            }
        }

        public static BitmapImage Star = new BitmapImage(new Uri(@"pack://application:,,,/icon/star.png"));
        public static BitmapImage HalfStar = new BitmapImage(new Uri(@"pack://application:,,,/icon/star-half-empty.png"));
        public static BitmapImage EmptyStar = new BitmapImage(new Uri(@"pack://application:,,,/icon/star-empty.png"));
        public ObservableCollection<BitmapImage> Stars { get; set; }

        public int StarSize { get; set; }

        public ScoreControl()
        {
            InitializeComponent();
            Stars = new ObservableCollection<BitmapImage>
            {
                EmptyStar,
                EmptyStar,
                EmptyStar,
                EmptyStar,
                EmptyStar
            };
            ChangeStarsByScore();
        }

        public void ChangeStarsByScore()
        {
            int i;
            for (i = 0; i < Score / 2; i++)
            {
                Stars[i] = Star;
            }

            if (Score % 2 == 1)
            {
                Stars[i++] = HalfStar;
            }

            for (; i < 5; i++)
            {
                Stars[i] = EmptyStar;
            }
        }
    }
}
