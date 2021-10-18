using System.Windows;
using System.Windows.Media;

namespace Otokoneko.Client.WPFClient.Utils
{
    public static class DependencyObjectExtensions
    {
        public static T GetChild<T>(this DependencyObject o)
        {
            if (o is T s) return s;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var child = VisualTreeHelper.GetChild(o, i);
                var result = GetChild<T>(child);
                if (result == null) continue;
                return result;
            }
            return default;
        }
    }
}
