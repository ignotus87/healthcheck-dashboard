namespace HealthcheckDashboard.TaskNS
{
    interface ITask
    {
        public string Name { get; }

        System.Threading.Tasks.Task PerformAsync();
    }
}
