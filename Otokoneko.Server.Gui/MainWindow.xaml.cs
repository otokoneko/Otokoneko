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
            if(!ConsoleControl.ProcessInterface.IsProcessRunning)
                ConsoleControl.StartProcess(App.CoreApp, "");
        }

        public void Exit()
        {
            if (ConsoleControl.ProcessInterface.IsProcessRunning)
                ConsoleControl.StopProcess();
            Close();
        }
    }
}
