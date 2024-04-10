using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcheckDashboard.ConditionNS;
using HealthcheckDashboard.ResourceNS;
using HealthcheckDashboard.ScheduleNS;
using HealthcheckDashboard.TaskNS;

namespace HealthcheckDashboard
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await ConfigureAndRun();

            await Console.Out.WriteLineAsync("FINISHED");
        }

        public static async Task ConfigureAndRun()
        {
            await Console.Out.WriteLineAsync("CONFIGURE");

            Resource resource = new GeneralFileResource(@"C:\tmp\testfile.txt");
            ITask task = new GetFileLastModifiedDateTask(resource as GeneralFileResource);
            Schedule schedule = new Schedule(new TimeSpan(0, 0, 10));
            ICondition<DateTime> condition = new DateTimeNotOlderThanTimeSpanCondition(new TimeSpan(0, 1, 0));

            var periodicTimer = new PeriodicTimer(schedule.TimeSpan);

            await RunInBackground(schedule.TimeSpan, () =>
            {
                Console.Out.WriteLineAsync("RUN");

                task.Perform();
                var value = (task as GetFileLastModifiedDateTask).LastModifiedDate;
                var result = condition.EvaluateCondition(value);

                Console.Out.WriteLineAsync($"Performed Task: {task}\nResource: {resource}\nValue: {value}\nCondition: {condition}\nResult: {result}");
            });
        }

        public static async Task RunInBackground(TimeSpan timeSpan, Action action)
        {
            var periodicTimer = new PeriodicTimer(timeSpan);
            while (await periodicTimer.WaitForNextTickAsync())
            {
                action();
            }
        }
    }
}
