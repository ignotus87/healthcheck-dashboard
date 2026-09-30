using HealthcheckDashboard.ConditionNS;
using HealthcheckDashboard.TaskNS;
using System;

namespace HealthcheckDashboard.ViewModel
{
    public class TaskItemViewModel
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
        public string Message { get; set; }
    }
}
