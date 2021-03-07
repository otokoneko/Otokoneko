using System.Windows;
using System.Windows.Controls;

namespace Otokoneko.Client.WPFClient.View
{
    class AlwaysRefreshDataTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var declaredDataTemplate = FindDeclaredDataTemplate(item, container);
            if (declaredDataTemplate == null) return null;
            var wrappedDataTemplate = WrapDataTemplate(declaredDataTemplate);
            return wrappedDataTemplate;
        }

        private static DataTemplate WrapDataTemplate(DataTemplate declaredDataTemplate)
        {
            var frameworkElementFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            frameworkElementFactory.SetValue(ContentPresenter.ContentTemplateProperty, declaredDataTemplate);
            var dataTemplate = new DataTemplate { VisualTree = frameworkElementFactory };
            return dataTemplate;
        }

        private static DataTemplate FindDeclaredDataTemplate(object item, DependencyObject container)
        {
            if (item == null) return null;
            var dataTemplateKey = new DataTemplateKey(item.GetType());
            var dataTemplate = ((FrameworkElement)container).FindResource(dataTemplateKey) as DataTemplate;
            return dataTemplate;
        }
    }
}
