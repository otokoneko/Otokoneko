using ConsoleControlAPI;
using Otokoneko.Server.Gui.Utils;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Otokoneko.Server.Gui
{

    /// <summary>
    /// ConsoleDisplayer.xaml 的交互逻辑
    /// </summary>
    public partial class ConsoleDisplayer : UserControl
    {
        private AnsiFont CurrentFont { get; set; } = AnsiEscapeCodeUtils.DefaultFont;

        private readonly ProcessInterface processInterface = new ProcessInterface();
        
        public bool IsProcessRunning { get; private set; }

        public ConsoleDisplayer()
        {
            InitializeComponent();

            processInterface.OnProcessOutput += ProcessInterface_OnProcessOutput;
            processInterface.OnProcessError += ProcessInterface_OnProcessError;
            processInterface.OnProcessExit += ProcessInterface_OnProcessExit;
        }

        private void ProcessInterface_OnProcessError(object sender, ProcessEventArgs args)
        {
            WriteOutput(args.Content, Colors.Red, Colors.Black);
        }

        private void ProcessInterface_OnProcessOutput(object sender, ProcessEventArgs args)
        {
            WriteOutput(args.Content, null, null);
        }

        private void ProcessInterface_OnProcessExit(object sender, ProcessEventArgs args)
        {
            Dispatcher.Invoke(delegate
            {
                Output.IsReadOnly = true;
                IsProcessRunning = false;
            });
        }

        public void WriteOutput(string output, Color? foreground, Color? background)
        {
            var texts = AnsiEscapeCodeUtils.ParseAnsiEscapeColor(output, CurrentFont);
            CurrentFont = texts.Last().Font;
            Dispatcher.Invoke(delegate
            {
                foreach (var text in texts)
                {
                    if (string.IsNullOrEmpty(text.Text)) continue;

                    TextRange textRange = new TextRange(Output.GetEndPointer(), Output.GetEndPointer());
                    textRange.Text = text.Text;
                    
                    textRange.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(foreground ?? text.Font.Foreground));
                    textRange.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(background ?? text.Font.Background));
                    Output.ScrollToEnd();
                    Output.SetCaretToEnd();
                }
            });
        }

        public void ClearOutput()
        {
            Output.Document.Blocks.Clear();
        }

        public void StartProcess(string fileName, string arguments)
        {
            StartProcess(new ProcessStartInfo(fileName, arguments));
        }

        public void StartProcess(ProcessStartInfo processStartInfo)
        {
            processInterface.StartProcess(processStartInfo);
            Dispatcher.Invoke(delegate
            {
                IsProcessRunning = true;
            });
        }

        public void StopProcess()
        {
            processInterface.StopProcess();
        }

        public void ScrollToEnd()
        {
            Output.ScrollToEnd();
        }
    }
}
