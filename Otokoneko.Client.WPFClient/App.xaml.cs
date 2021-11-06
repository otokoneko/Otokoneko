using System;
using System.Windows;

namespace Otokoneko.Client.WPFClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += Application_DispatcherUnhandledException;
            base.OnStartup(e);
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            var crashReporter = new View.CrashReporter("出现未捕获的异常", e.Exception);
            crashReporter.ShowDialog();
            Environment.Exit(-1);
        }
    }
}
