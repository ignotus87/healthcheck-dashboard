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

        private class IconUpdateRequest
        {
            public TaskState State;
        }

        public enum TaskState
        {
            Unvalidated,  // Yellow ?
            Success,      // Green checkmark
            Error         // Red X
        }

        private static readonly BlockingCollection<Notification> _queue = new BlockingCollection<Notification>();
        private static readonly BlockingCollection<IconUpdateRequest> _iconQueue = new BlockingCollection<IconUpdateRequest>();
        private static Thread _uiThread;
        private static volatile bool _initialized = false;
        private static NotifyIcon _notifyIcon;

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

        /// <summary>
        /// Updates the taskbar icon based on task state.
        /// </summary>
        public static void UpdateTaskbarIcon(TaskState state)
        {
            if (!_initialized) Initialize();
            _iconQueue.Add(new IconUpdateRequest { State = state });
        }

        private static void RunUi()
        {
            // Prepare WinForms UI thread
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            _notifyIcon = new NotifyIcon
            {
                Icon = CreateIconForState(TaskState.Unvalidated),
                Visible = true,
                Text = "Healthcheck Dashboard"
            };

            // Create context menu
            var contextMenu = new ContextMenuStrip();
            var openMenuItem = new ToolStripMenuItem("Open Dashboard", null, (s, e) =>
            {
                TaskStatusWindow statusWindow = new TaskStatusWindow();
                statusWindow.Show();
            });
            contextMenu.Items.Add(openMenuItem);
            var exitMenuItem = new ToolStripMenuItem("Exit", null, (s, e) =>
            {
                _notifyIcon.Visible = false;
                Environment.Exit(0);
            });
            contextMenu.Items.Add(exitMenuItem);
            _notifyIcon.ContextMenuStrip = contextMenu;

            // List of visible notification windows (managed on UI thread)
            var openForms = new List<(NotificationForm Form, DateTime ShownAt, int TimeoutMs)>();
            var margin = 8;

            // UI timer polls the queue on the UI thread and creates persistent notification windows.
            using var timer = new System.Windows.Forms.Timer();
            timer.Interval = 200;
            timer.Tick += (s, e) =>
            {
                try
                {
                    // Process icon update requests
                    while (_iconQueue.TryTake(out var iconReq))
                    {
                        if (_notifyIcon != null)
                        {
                            var oldIcon = _notifyIcon.Icon;
                            _notifyIcon.Icon = CreateIconForState(iconReq.State);
                            oldIcon?.Dispose();
                        }
                    }

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
                            var idx = openForms.FindIndex(t => t.Form == form);
                            if (idx >= 0) openForms.RemoveAt(idx);
                            for (int i = 0; i < openForms.Count; i++)
                            {
                                var f = openForms[i].Form;
                                var newY = wa.Bottom - ((i + 1) * (f.Height + margin));
                                f.Location = new Point(wa.Right - f.Width - margin, newY);
                            }
                        };

                        openForms.Add((form, DateTime.UtcNow, n.TimeoutMs));
                        form.Show();
                    }

                    // Close forms that have been open longer than their TimeoutMs
                    if (openForms.Count > 0)
                    {
                        var now = DateTime.UtcNow;
                        var toClose = new List<NotificationForm>();
                        foreach (var entry in openForms)
                        {
                            if (entry.TimeoutMs > 0 && (now - entry.ShownAt).TotalMilliseconds >= entry.TimeoutMs)
                            {
                                toClose.Add(entry.Form);
                            }
                        }

                        foreach (var f in toClose)
                        {
                            try
                            {
                                // Closing triggers FormClosed handler which will remove and reposition remaining forms
                                f.Close();
                            }
                            catch
                            {
                                // swallow
                            }
                        }
                    }

                    // If queue was marked complete and empty, exit UI thread
                    if (_queue.IsAddingCompleted && _queue.Count == 0 && openForms.Count == 0)
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
                _notifyIcon.Visible = false;
                _notifyIcon?.Dispose();
            }
        }

        private static Icon CreateIconForState(TaskState state)
        {
            const int size = 16;
            var bitmap = new Bitmap(size, size);

            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);

                switch (state)
                {
                    case TaskState.Error:
                        // Red background with white X
                        using (var redBrush = new SolidBrush(Color.Red))
                        {
                            g.FillEllipse(redBrush, 0, 0, size, size);
                        }
                        using (var whitePen = new Pen(Color.White, 2))
                        {
                            g.DrawLine(whitePen, 3, 3, size - 3, size - 3);
                            g.DrawLine(whitePen, size - 3, 3, 3, size - 3);
                        }
                        break;

                    case TaskState.Success:
                        // Green background with white checkmark
                        using (var greenBrush = new SolidBrush(Color.Green))
                        {
                            g.FillEllipse(greenBrush, 0, 0, size, size);
                        }
                        using (var whitePen = new Pen(Color.White, 2))
                        {
                            // Draw checkmark
                            var points = new Point[]
                            {
                                new Point(4, 8),
                                new Point(7, 12),
                                new Point(13, 4)
                            };
                            g.DrawLines(whitePen, points);
                        }
                        break;

                    case TaskState.Unvalidated:
                    default:
                        // Yellow background with question mark
                        using (var yellowBrush = new SolidBrush(Color.Gold))
                        {
                            g.FillEllipse(yellowBrush, 0, 0, size, size);
                        }
                        using (var font = new Font("Arial", 10, FontStyle.Bold))
                        using (var blackBrush = new SolidBrush(Color.Black))
                        {
                            var textSize = g.MeasureString("?", font);
                            var x = (size - textSize.Width) / 2;
                            var y = (size - textSize.Height) / 2 - 1;
                            g.DrawString("?", font, blackBrush, x, y);
                        }
                        break;
                }
            }

            return Icon.FromHandle(bitmap.GetHicon());
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
                _iconQueue.CompleteAdding();
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