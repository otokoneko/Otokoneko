using System.Windows;

namespace Otokoneko.Server.Gui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            if (!ConsoleControl.IsProcessRunning)
                ConsoleControl.StartProcess(App.CoreApp, "--ansicolor");

            Activated += OnActivated;
        }

        private void OnActivated(object sender, System.EventArgs e)
        {
            ConsoleControl.ScrollToEnd();
        }

        public void Exit()
        {
            if (ConsoleControl.IsProcessRunning)
                ConsoleControl.StopProcess();

            Close();
        }
    }
}
