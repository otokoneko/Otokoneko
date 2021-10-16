﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;

namespace Otokoneko.Server.Gui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string CoreApp = "Otokoneko.Server.exe";
        private MainWindow Window { get; set; }
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private bool _isExit;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Window = new MainWindow();
            MainWindow = Window;
            MainWindow.Closing += MainWindow_Closing;

            MainWindow.Show();

            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(CoreApp)
            };
            _notifyIcon.DoubleClick += (s, args) => ShowMainWindow();
            _notifyIcon.Visible = true;

            CreateContextMenu();
        }

        private void CreateContextMenu()
        {
            _notifyIcon.ContextMenuStrip =
              new System.Windows.Forms.ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("清空输出").Click += (s, e) => Window.ClearOutput();
            _notifyIcon.ContextMenuStrip.Items.Add("打开日志目录").Click += (s, e) => OpenFolder("./log");
            _notifyIcon.ContextMenuStrip.Items.Add("打开插件目录").Click += (s, e) => OpenFolder("./plugins");
            _notifyIcon.ContextMenuStrip.Items.Add("打开主界面").Click += (s, e) => ShowMainWindow();
            _notifyIcon.ContextMenuStrip.Items.Add("退出").Click += (s, e) => ExitApplication();
        }

        private void ExitApplication()
        {
            if (!_isExit)
            {
                _isExit = true;
                Window.Exit();
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
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

        private void ShowMainWindow()
        {
            if (MainWindow.IsVisible)
            {
                if (MainWindow.WindowState == WindowState.Minimized)
                {
                    MainWindow.WindowState = WindowState.Normal;
                }
                MainWindow.Activate();
            }
            else
            {
                MainWindow.Show();
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!_isExit)
            {
                e.Cancel = true;
                MainWindow.Hide();
            }
        }
    }
}
