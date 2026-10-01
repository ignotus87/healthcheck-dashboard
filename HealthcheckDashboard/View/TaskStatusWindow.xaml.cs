using HealthcheckDashboard.ViewModel;
using System;
using System.Windows;
using System.Windows.Threading;

namespace HealthcheckDashboard
{
    public partial class TaskStatusWindow : Window
    {
        private static TaskStatusWindow _instance;
        private static readonly object _lock = new object();
        private TaskStatusViewModel _viewModel;
        private DispatcherTimer _refreshTimer;

        /// <summary>
        /// Gets or creates a singleton instance of the TaskStatusWindow.
        /// Only one window can exist at a time.
        /// </summary>
        public static TaskStatusWindow GetInstance()
        {
            lock (_lock)
            {
                // If no instance exists or the window was closed, create a new one
                if (_instance == null || _instance.IsLoaded == false)
                {
                    _instance = new TaskStatusWindow();
                }
                return _instance;
            }
        }

        public TaskStatusWindow()
        {
            InitializeComponent();
            _viewModel = new TaskStatusViewModel();
            this.DataContext = _viewModel;

            // Initialize the refresh timer
            InitializeRefreshTimer();

            // Wire up the Loaded event to start the timer
            this.Loaded += TaskStatusWindow_Loaded;
        }

        private void InitializeRefreshTimer()
        {
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(10);
            _refreshTimer.Tick += RefreshTimer_Tick;
        }

        private void TaskStatusWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Start the refresh timer when the window is loaded
            _refreshTimer?.Start();
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            // Refresh the view model with current task data
            _viewModel.RefreshTasks();

            // Update the last update time label
            UpdateTimeLabel.Text = $"Last updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }

        public void UpdateTaskStatus(TaskItemViewModel taskStatus)
        {
            _viewModel.UpdateTask(taskStatus);
        }

        public void AddTask(TaskItemViewModel taskStatus)
        {
            _viewModel.AddTask(taskStatus);
        }

        public void ClearTasks()
        {
            _viewModel.ClearTasks();
        }

        /// <summary>
        /// Execute the selected task immediately with full property updates
        /// </summary>
        private async void ExecuteNow_Click(object sender, RoutedEventArgs e)
        {
            var selectedTask = TaskStatusDataGrid.SelectedItem as TaskItemViewModel;
            if (selectedTask == null) return;

            // Find the task instance ID from TaskItems
            var taskIndex = Program.TaskItems.FindIndex(t => t.Task.Name == selectedTask.Task.Name);
            if (taskIndex < 0) return;

            // Execute task with full property updates (same as background runner)
            await Program.ExecuteTaskNow(selectedTask, taskIndex);
        }

        /// <summary>
        /// Clear the instance when the window is closed
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _refreshTimer?.Stop();
            base.OnClosed(e);
            lock (_lock)
            {
                _instance = null;
                this.Close();
            }
        }
    }
}