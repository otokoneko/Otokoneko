using Otokoneko.Server.Gui.Utils;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Otokoneko.Server.Gui
{
    public partial class ConsoleDisplayer : UserControl
    {
        private int MaxOutputLength => 256 * 1024;

        public event EventHandler<bool> OnProcessChanged;

        private readonly BlockingCollection<Tuple<string, Color?, Color?>> OutputQueue = new();
        private AnsiFont CurrentFont { get; set; } = AnsiEscapeCodeUtils.DefaultFont;

        private readonly ProcessInterface processInterface = new ProcessInterface();
        public bool IsProcessRunning { get; private set; }

        public ConsoleDisplayer()
        {
            InitializeComponent();

            Load += OnLoad;
            processInterface.OnProcessOutput += ProcessInterface_OnProcessOutput;
            processInterface.OnProcessError += ProcessInterface_OnProcessError;
            processInterface.OnProcessExit += ProcessInterface_OnProcessExit;

            Task.Run(WriteOutput);
        }
        
        private void ProcessInterface_OnProcessError(object sender, ProcessEventArgs args)
        {
            OutputQueue.Add(new Tuple<string, Color?, Color?>(args.Content, Color.Red, Color.Black));
        }

        private void ProcessInterface_OnProcessOutput(object sender, ProcessEventArgs args)
        {
            OutputQueue.Add(new Tuple<string, Color?, Color?>(args.Content, null, null));
        }

        private void ProcessInterface_OnProcessExit(object sender, ProcessEventArgs args)
        {
            Invoke((Action)delegate
            {
                Output.ReadOnly = true;
            });
            IsProcessRunning = false;
            OnProcessChanged?.Invoke(this, IsProcessRunning);
            Thread.Sleep(100);
            OutputQueue.Add(new Tuple<string, Color?, Color?>($"\n服务进程退出，退出代码为 {args.Code}\n", Color.White, Color.Black));
        }

        public void WriteOutput()
        {
            Tuple<string, Color?, Color?> args = null;
            while (true)
            {
                do
                {
                    var top = OutputQueue.Take();
                    if (args == null) args = top;
                    else
                    {
                        if (top.Item2 == args.Item2 && top.Item3 == args.Item3)
                        {
                            args = new Tuple<string, Color?, Color?>(args.Item1 + top.Item1, args.Item2, args.Item3);
                        }
                        else
                        {
                            Write(args.Item1, args.Item2, args.Item3);
                            args = top;
                        }
                    }
                } while (OutputQueue.Count != 0);

                if (args != null)
                {
                    Write(args.Item1, args.Item2, args.Item3);
                    args = null;
                }
            }
        }

        private void Write(string output, Color? foreground, Color? background)
        {
            var texts = AnsiEscapeCodeUtils.ParseAnsiEscapeColor(output, CurrentFont);
            CurrentFont = texts.Last().Font;
            Invoke((Action)delegate
            {
                foreach (var text in texts)
                {
                    if (string.IsNullOrEmpty(text.Text)) continue;

                    Output.SelectionStart = Output.Text.Length;
                    Output.SelectionLength = 0;
                    Output.SelectionColor = foreground ?? text.Font.Foreground;
                    Output.SelectionBackColor = background ?? text.Font.Background;
                    Output.AppendText(text.Text);
                }

                if (Output.Text.Length > 1.05 * MaxOutputLength)
                {
                    var size = Output.Text.Length - MaxOutputLength;
                    for (; size < Output.Text.Length; size++)
                    {
                        if (Output.Text[size - 1] == '\n') break;
                    }
                    Output.ReadOnly = false;
                    Output.Select(0, size);
                    Output.SelectedText = "";
                    Output.ReadOnly = true;
                }
            });
        }

        public void ClearOutput()
        {
            Output.Clear();
        }

        public void StartProcess(string fileName, string arguments)
        {
            StartProcess(new ProcessStartInfo(fileName, arguments));
        }

        public void StartProcess(ProcessStartInfo processStartInfo)
        {
            processInterface.StartProcess(processStartInfo);
            IsProcessRunning = true;
            OnProcessChanged?.Invoke(this, IsProcessRunning);
        }

        public void StopProcess()
        {
            processInterface.WriteInput("exit");
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);
        private const int WM_VSCROLL = 277;
        private const int SB_PAGEBOTTOM = 7;

        public void ScrollToEnd()
        {
            SendMessage(Output.Handle, WM_VSCROLL, (IntPtr)SB_PAGEBOTTOM, IntPtr.Zero);
            Output.SelectionStart = Output.Text.Length;
        }

        private void OnLoad(object sender, EventArgs e)
        {
            Invoke((Action)delegate
            {
                Output.AutoWordSelection = false;
                Output.LanguageOption = RichTextBoxLanguageOptions.UIFonts;
            });
        }
    }
}
