using System;
using System.Windows.Forms;
using System.Drawing;
using System.Windows;

namespace HealthcheckDashboard
{
    public class TrayIconManager : IDisposable
    {
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;

        public TrayIconManager()
        {
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application, // Replace with your custom icon
                Visible = true,
                Text = "Healthcheck Dashboard"
            };

            // Create context menu
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Items.Add("Show", null, (s, e) => ShowMainWindow());
            _contextMenu.Items.Add("Hide", null, (s, e) => HideMainWindow());
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

            _notifyIcon.ContextMenuStrip = _contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        }

        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            // Handle single click if needed
            if (e.Button == MouseButtons.Left)
            {
                ShowMainWindow();
            }
        }

        private void ShowMainWindow()
        {
            var window = TaskStatusWindow.GetInstance();
            window.Show();
            window.Activate();
            window.WindowState = WindowState.Normal;
        }

        private void HideMainWindow()
        {
            var window = TaskStatusWindow.GetInstance();
            if (window.IsLoaded)
            {
                window.WindowState = WindowState.Minimized;
                window.Hide();
            }
        }

        private void ExitApplication()
        {
            System.Windows.Application.Current.Shutdown();
        }

        public void Dispose()
        {
            _contextMenu?.Dispose();
            _notifyIcon?.Dispose();
        }
    }
}