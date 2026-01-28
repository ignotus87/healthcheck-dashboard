using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

namespace HealthcheckDashboard
{
    // Small helper that runs a NotifyIcon on a dedicated STA thread and accepts notify requests.
    internal static class DesktopNotifier
    {
        private class Notification
        {
            public string Title;
            public string Text;
            public ToolTipIcon Icon;
            public int TimeoutMs;
        }

        private static readonly BlockingCollection<Notification> _queue = new BlockingCollection<Notification>();
        private static Thread _uiThread;
        private static volatile bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;
            lock (_queue)
            {
                if (_initialized) return;
                _uiThread = new Thread(RunUi) { IsBackground = true };
                _uiThread.SetApartmentState(ApartmentState.STA);
                _uiThread.Start();
                _initialized = true;
            }
        }

        public static void Notify(string title, string text, ToolTipIcon icon = ToolTipIcon.Info, int timeoutMs = 5000)
        {
            if (!_initialized) Initialize();
            _queue.Add(new Notification { Title = title, Text = text, Icon = icon, TimeoutMs = timeoutMs });
        }

        private static void RunUi()
        {
            using var notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "Healthcheck Dashboard"
            };

            try
            {
                foreach (var n in _queue.GetConsumingEnumerable())
                {
                    try
                    {
                        // ShowBalloonTip expects timeout in milliseconds (int)
                        notifyIcon.ShowBalloonTip(Math.Max(1000, n.TimeoutMs), n.Title, n.Text, n.Icon);
                    }
                    catch
                    {
                        // swallow individual notification errors
                    }
                }
            }
            finally
            {
                notifyIcon.Visible = false;
            }
        }

        public static void Shutdown()
        {
            // stop accepting new notifications and allow UI thread to finish
            try
            {
                _queue.CompleteAdding();
                if (_uiThread != null && !_uiThread.Join(2000))
                {
                    _uiThread.Interrupt();
                }
            }
            catch { }
        }
    }
}