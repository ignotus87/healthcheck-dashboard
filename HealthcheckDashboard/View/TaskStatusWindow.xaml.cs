using HealthcheckDashboard.ViewModel;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace HealthcheckDashboard
{
    public partial class TaskStatusWindow : Window
    {
        private TaskStatusViewModel _viewModel;

        public TaskStatusWindow()
        {
            InitializeComponent();
            _viewModel = new TaskStatusViewModel();
            this.DataContext = _viewModel;
        }

        public void UpdateTaskStatus(TaskItemViewModel taskStatus)
        {
            _viewModel.UpdateTask(taskStatus);
            //UpdateTimeLabel.Text = $"Last updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }

        public void AddTask(TaskItemViewModel taskStatus)
        {
            _viewModel.AddTask(taskStatus);
        }

        public void ClearTasks()
        {
            _viewModel.ClearTasks();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            //UpdateTimeLabel.Text = $"Last updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearTasks();
        }
    }
}