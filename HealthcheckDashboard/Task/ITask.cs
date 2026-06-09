namespace HealthcheckDashboard.TaskNS
{
    interface ITask
    {
        public string Name { get; }
        public bool IsEnabled { get; }

        System.Threading.Tasks.Task PerformAsync();
    }
}
