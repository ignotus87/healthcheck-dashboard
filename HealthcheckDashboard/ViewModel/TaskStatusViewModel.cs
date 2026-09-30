using HealthcheckDashboard.ViewModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HealthcheckDashboard
{
    public class TaskStatusViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<TaskItemViewModel> _tasks;

        public ObservableCollection<TaskItemViewModel> Tasks
        {
            get => _tasks;
            set { _tasks = value; OnPropertyChanged(); }
        }

        public TaskStatusViewModel()
        {
            Tasks = new ObservableCollection<TaskItemViewModel>(Program.TaskItems);
        }

        public void AddTask(TaskItemViewModel task)
        {
            if (task == null) return;
            Tasks.Add(task);
        }

        public void UpdateTask(TaskItemViewModel updatedTask)
        {
            if (updatedTask == null) return;

            var existing = Tasks.FirstOrDefault(t => t.Task.Name == updatedTask.Task.Name);
            if (existing != null)
            {
                existing.LastRunTime = updatedTask.LastRunTime;
                existing.NextRunTime = updatedTask.NextRunTime;
                existing.Message = updatedTask.Message;
            }
            else
            {
                Tasks.Add(updatedTask);
            }
        }

        public void ClearTasks()
        {
            Tasks.Clear();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}