using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            // Prepare WinForms UI thread
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "Healthcheck Dashboard"
            };

            // List of visible notification windows (managed on UI thread)
            var openForms = new List<NotificationForm>();
            var margin = 8;

            // UI timer polls the queue on the UI thread and creates persistent notification windows.
            using var timer = new System.Windows.Forms.Timer();
            timer.Interval = 200;
            timer.Tick += (s, e) =>
            {
                try
                {
                    // Show all queued notifications
                    while (_queue.TryTake(out var n))
                    {
                        var form = new NotificationForm(n.Title, n.Text, MapIcon(n.Icon));
                        // limit width to a reasonable value
                        var wa = Screen.PrimaryScreen.WorkingArea;
                        var maxWidth = Math.Min(420, wa.Width / 3);
                        form.Size = new Size(maxWidth, form.PreferredHeight);

                        // calculate stacked position (bottom-right, stack upwards)
                        var x = wa.Right - form.Width - margin;
                        var y = wa.Bottom - ((openForms.Count + 1) * (form.Height + margin));
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = new Point(x, y);

                        form.FormClosed += (fs, fe) =>
                        {
                            // reposition remaining forms
                            var idx = openForms.IndexOf(form);
                            if (idx >= 0) openForms.RemoveAt(idx);
                            for (int i = 0; i < openForms.Count; i++)
                            {
                                var f = openForms[i];
                                var newY = wa.Bottom - ((i + 1) * (f.Height + margin));
                                f.Location = new Point(wa.Right - f.Width - margin, newY);
                            }
                        };

                        openForms.Add(form);
                        form.Show();
                    }

                    // If queue was marked complete and empty, exit UI thread
                    if (_queue.IsAddingCompleted && _queue.Count == 0)
                    {
                        timer.Stop();
                        Application.ExitThread();
                    }
                }
                catch
                {
                    // swallow per-notification errors
                }
            };

            timer.Start();

            try
            {
                Application.Run();
            }
            finally
            {
                notifyIcon.Visible = false;
            }
        }

        private static Icon MapIcon(ToolTipIcon t)
        {
            return t switch
            {
                ToolTipIcon.Info => SystemIcons.Information,
                ToolTipIcon.Warning => SystemIcons.Warning,
                ToolTipIcon.Error => SystemIcons.Error,
                _ => SystemIcons.Application,
            };
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

        // Simple, persistent notification window that stays until closed by the user.
        private class NotificationForm : Form
        {
            private readonly Label _titleLabel;
            private readonly TextBox _textBox;
            private readonly Button _closeButton;
            private readonly PictureBox _iconBox;
            public int PreferredHeight => Math.Max(120, _textBox.PreferredHeight + 40);

            public NotificationForm(string title, string text, Icon icon)
            {
                FormBorderStyle = FormBorderStyle.FixedSingle;
                StartPosition = FormStartPosition.Manual;
                ShowInTaskbar = false;
                TopMost = true;
                MaximizeBox = false;

                _iconBox = new PictureBox
                {
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Size = new Size(32, 32),
                    Location = new Point(8, 8),
                    Image = icon.ToBitmap()
                };

                _titleLabel = new Label
                {
                    Text = title ?? string.Empty,
                    Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
                    AutoSize = false,
                    Location = new Point(48, 8),
                    Size = new Size(300, 18)
                };

                _textBox = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    BorderStyle = BorderStyle.None,
                    BackColor = SystemColors.Control,
                    Location = new Point(48, 28),
                    Size = new Size(300, 52),
                    Text = text ?? string.Empty,
                    ScrollBars = ScrollBars.Vertical
                };

                _closeButton = new Button
                {
                    Text = "Close",
                    Size = new Size(60, 24),
                    Location = new Point(48, 28 + _textBox.Height + 4),
                };
                _closeButton.Click += (s, e) => Close();

                // allow double-click anywhere to close
                this.DoubleClick += (s, e) => Close();
                _titleLabel.DoubleClick += (s, e) => Close();
                _textBox.DoubleClick += (s, e) => Close();
                _iconBox.DoubleClick += (s, e) => Close();

                Controls.Add(_iconBox);
                Controls.Add(_titleLabel);
                Controls.Add(_textBox);
                Controls.Add(_closeButton);

                // set a reasonable default size; caller may adjust
                Width = 360;
                Height = PreferredHeight + 8;
            }
        }
    }
}