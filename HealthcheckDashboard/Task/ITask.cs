namespace HealthcheckDashboard.TaskNS
{
    public interface ITask
    {
        public string Name { get; }
        public bool IsEnabled { get; }

        System.Threading.Tasks.Task PerformAsync();
    }
}
