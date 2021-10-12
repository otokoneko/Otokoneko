using Otokoneko.Client.WPFClient.Utils;
using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Otokoneko.Client.WPFClient.View
{
    public class SmoothScrollViewer : ScrollViewer
    {
        private double LastLocation = 0;
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            double WheelChange = e.Delta;
            double newOffset = LastLocation - (WheelChange * 1.4);
            newOffset = MathUtils.LimitValue(newOffset, 0, ScrollableHeight);

            ScrollToVerticalOffset(LastLocation);
            AnimateScroll(newOffset);

            LastLocation = newOffset;

            e.Handled = true;
        }
        private void AnimateScroll(double ToValue)
        {
            BeginAnimation(ScrollViewerBehavior.VerticalOffsetProperty, null);
            var Animation = new DoubleAnimation
            {
                EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut },
                From = VerticalOffset,
                To = ToValue,
                Duration = TimeSpan.FromMilliseconds(800)
            };

            //Timeline.SetDesiredFrameRate(Animation, 30);
            BeginAnimation(ScrollViewerBehavior.VerticalOffsetProperty, Animation);
        }
    }
}
