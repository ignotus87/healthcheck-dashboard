using HealthcheckDashboard.ViewModel;
using System;
using System.Windows;

namespace HealthcheckDashboard
{
    public partial class TaskStatusWindow : Window
    {
        private static TaskStatusWindow _instance;
        private static readonly object _lock = new object();
        private TaskStatusViewModel _viewModel;

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
        /// Clear the instance when the window is closed
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            base.OnClosed(e);
            lock (_lock)
            {
                _instance = null;
                this.Close();
            }
        }
    }
}