using HealthcheckDashboard.ConditionNS;
using HealthcheckDashboard.TaskNS;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Documents;

namespace HealthcheckDashboard.ViewModel
{
    public class TaskItemViewModel : INotifyPropertyChanged
    {
        public string Name => Task?.Name ?? "Unknown Task";
        public ITask Task { get; set; }
        public ResourceNS.Resource Resource { get; set; }
        public ICondition Condition { get; set; }
        public bool? LastResult { get; set; }
        public bool ShouldColorRed => LastResult.HasValue && LastResult.Value == Condition.WarnWhen;
        public string Status => LastResult.HasValue ? (LastResult.Value == Condition.WarnWhen ? "Warning" : "OK") : "Unknown";
        public DateTime LastRunTime { get; set; } = DateTime.MinValue;
        public DateTime NextRunTime { get; set; } = DateTime.MinValue;
        public string Message => Messages.LastOrDefault();
        public List<string> Messages { get; set; } = new List<string>();

        /// <summary>
        /// Refresh all UI-bound properties to trigger property change notifications
        /// </summary>
        public void RefreshBindings()
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(LastRunTime));
            OnPropertyChanged(nameof(NextRunTime));
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(ShouldColorRed));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}