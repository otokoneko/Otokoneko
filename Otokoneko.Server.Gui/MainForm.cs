using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Otokoneko.Server.Gui
{
    public partial class OtokonekoServer : Form
    {
        public static string CoreApp = "Otokoneko.Server.exe";
        private NotifyIcon _notifyIcon;
        private bool _isExit;

        private ToolStripMenuItem _autoStartMenuItem;
        private ToolStripMenuItem _startProcessMenuItem;
        private ToolStripMenuItem _stopProcessMenuItem;

        public OtokonekoServer()
        {
            InitializeComponent();
            
            CreateNotifyIcon();

            FormClosing += OnClosing;
            Load += OnLoad;
            Shown += OnShown;
        }

        private void OnShown(object sender, EventArgs e)
        {
            Output.ScrollToEnd();
        }

        private void OnLoad(object sender, EventArgs e)
        {
            Output.OnProcessChanged += OnProcessChanged;
            StartProcess();
        }

        private void OnProcessChanged(object sender, bool running)
        {
            Invoke((Action)delegate
            {
                _startProcessMenuItem.Enabled = !running;
                _stopProcessMenuItem.Enabled = running;
            });
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (_isExit) return;
            e.Cancel = true;
            Hide();
        }

        private void Exit()
        {
            if (_isExit) return;
            _isExit = true;
            _notifyIcon.Visible = false;
            StopProcess();
            Close();
        }

        private void ShowAndActive()
        {
            Show();
            Activate();
        }

        private void CreateNotifyIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(CoreApp),
                Visible = true,
            };

            var contextMenuStrip = new ContextMenuStrip();

            _notifyIcon.DoubleClick += (s, args) => ShowAndActive();

            _autoStartMenuItem = new ToolStripMenuItem("开机启动", null, (s, e) => { AutoStart = !AutoStart; });

            _startProcessMenuItem = new ToolStripMenuItem("启动服务", null, (s, e) => StartProcess());
            _stopProcessMenuItem = new ToolStripMenuItem("停止服务", null, (s, e) => StopProcess());

            contextMenuStrip.Items.Add(_autoStartMenuItem);
            contextMenuStrip.Items.Add("-");
            contextMenuStrip.Items.Add(_startProcessMenuItem);
            contextMenuStrip.Items.Add(_stopProcessMenuItem);
            contextMenuStrip.Items.Add("-");
            contextMenuStrip.Items.Add("清空输出").Click += (s, e) => Output.ClearOutput();
            contextMenuStrip.Items.Add("-");
            contextMenuStrip.Items.Add("打开日志目录").Click += (s, e) => OpenFolder("./log");
            contextMenuStrip.Items.Add("打开插件目录").Click += (s, e) => OpenFolder("./plugins");
            contextMenuStrip.Items.Add("打开主界面").Click += (s, e) => ShowAndActive();
            contextMenuStrip.Items.Add("-");
            contextMenuStrip.Items.Add("退出").Click += (s, e) => Exit();

            _autoStartMenuItem.Checked = AutoStart;

            _notifyIcon.ContextMenuStrip = contextMenuStrip;
        }

        private const string _autostartKey = "otokoneko.server.gui";

        private string ExecutablePath => Path.ChangeExtension(Application.ExecutablePath, "exe");

        public bool AutoStart
        {
            get
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                return key.GetValue(_autostartKey) != null;
            }
            set
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (value && key.GetValue(_autostartKey) == null)
                {
                    key.SetValue(_autostartKey, ExecutablePath);
                }
                else if (!value && key.GetValue(_autostartKey) != null)
                {
                    key.DeleteValue(_autostartKey);
                }
                _autoStartMenuItem.Checked = AutoStart;
            }
        }

        private void StartProcess()
        {
            if(!Output.IsProcessRunning)
                Output.StartProcess(CoreApp, "--ansicolor");
        }

        private void StopProcess()
        {
            if (Output.IsProcessRunning)
                Output.StopProcess();
        }

        private void OpenFolder(string folderPath)
        {
            folderPath = Path.GetFullPath(folderPath);
            if (Directory.Exists(folderPath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    Arguments = folderPath,
                    FileName = "explorer.exe"
                };

                Process.Start(startInfo);
            }
        }
    }
}
