using Otokoneko.Client.WPFClient.Utils;
using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Otokoneko.Client.WPFClient.View
{
    public class SmoothScrollViewer : ScrollViewer
    {
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            double newOffset = VerticalOffset - (e.Delta * 2);
            newOffset = MathUtils.LimitValue(newOffset, 0, ScrollableHeight);
            AnimateScroll(newOffset);
            e.Handled = true;
        }
        private void AnimateScroll(double ToValue)
        {
            BeginAnimation(ScrollViewerBehavior.VerticalOffsetProperty, null);
            var Animation = new DoubleAnimation
            {
                EasingFunction = new SineEase() { EasingMode = EasingMode.EaseOut },
                From = VerticalOffset,
                To = ToValue,
                Duration = TimeSpan.FromMilliseconds(500)
            };
            BeginAnimation(ScrollViewerBehavior.VerticalOffsetProperty, Animation);
        }
    }
}
