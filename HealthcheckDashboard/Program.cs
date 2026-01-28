using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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

            // locate configuration file (next to the running executable)
            var configPath = Path.Combine(AppContext.BaseDirectory, "tasks.json");
            if (!File.Exists(configPath))
            {
                await Console.Error.WriteLineAsync($"Configuration file not found: {configPath}");
                return;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(File.ReadAllText(configPath));
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Failed to parse configuration: {ex.Message}");
                return;
            }

            if (!doc.RootElement.TryGetProperty("tasks", out var tasksElement) || tasksElement.ValueKind != JsonValueKind.Array)
            {
                await Console.Error.WriteLineAsync("Configuration does not contain a 'tasks' array.");
                return;
            }

            var backgroundTasks = new List<Task>();

            foreach (var taskConfig in tasksElement.EnumerateArray())
            {
                try
                {
                    var taskName = taskConfig.TryGetProperty("name", out var n) ? n.GetString() : "(unnamed)";

                    // Create resource
                    var resource = CreateResource(taskConfig.GetProperty("resource"));

                    // Create task
                    var task = CreateTask(taskConfig.GetProperty("taskType").GetString(), resource);

                    // Create schedule
                    var scheduleElement = taskConfig.GetProperty("schedule");
                    var intervalSeconds = scheduleElement.TryGetProperty("intervalSeconds", out var s) ? s.GetInt32() : 60;
                    var schedule = new Schedule(TimeSpan.FromSeconds(intervalSeconds));

                    // Create condition (optional)
                    ICondition<DateTime> condition = null;
                    if (taskConfig.TryGetProperty("condition", out var conditionElement))
                    {
                        condition = CreateCondition(conditionElement);
                    }

                    // Start background runner for this configured task
                    var bgTask = RunInBackground(schedule.TimeSpan, () =>
                    {
                        // run the configured task and evaluate condition if provided
                        try
                        {
                            task.Perform();

                            if (task is GetFileLastModifiedDateTask gf)
                            {
                                var value = gf.LastModifiedDate;
                                var result = condition != null ? condition.EvaluateCondition(value) : false;
                                Console.Out.WriteLineAsync($"[{taskName}] Condition: {condition}\nResult: {result}");
                            }
                            else
                            {
                                // generic fallback
                                Console.Out.WriteLineAsync($"[{taskName}] Performed Task: {task}\nResource: {resource}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLineAsync($"[{taskName}] Task execution error: {ex}");
                        }
                    });

                    backgroundTasks.Add(bgTask);
                    await Console.Out.WriteLineAsync($"Started configured task: {taskName}");
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync($"Failed to start a configured task: {ex.Message}");
                }
            }

            // Wait for all background runners (they are long-running)
            await Task.WhenAll(backgroundTasks);
        }

        // Minimal factory helpers - extend as you add more ITask/IResource/ICondition types
        private static Resource CreateResource(JsonElement resourceElement)
        {
            var resourceType = resourceElement.GetProperty("resourceType").GetString();

            switch (resourceType)
            {
                case "GeneralFileResource":
                    var filePath = resourceElement.GetProperty("filePath").GetString();
                    return new GeneralFileResource(filePath);
                default:
                    throw new NotSupportedException($"Resource type not supported: {resourceType}");
            }
        }

        private static ITask CreateTask(string taskType, Resource resource)
        {
            switch (taskType)
            {
                case "GetFileLastModifiedDateTask":
                    if (resource is GeneralFileResource gfr)
                        return new GetFileLastModifiedDateTask(gfr);
                    throw new ArgumentException("GetFileLastModifiedDateTask requires a GeneralFileResource");
                default:
                    throw new NotSupportedException($"Task type not supported: {taskType}");
            }
        }

        private static ICondition<DateTime> CreateCondition(JsonElement conditionElement)
        {
            var conditionType = conditionElement.GetProperty("conditionType").GetString();
            switch (conditionType)
            {
                case "DateTimeNotOlderThanTimeSpanCondition":
                    var notOlderSeconds = conditionElement.TryGetProperty("notOlderThanSeconds", out var s) ? s.GetInt32() : 60;
                    return new DateTimeNotOlderThanTimeSpanCondition(TimeSpan.FromSeconds(notOlderSeconds));
                default:
                    throw new NotSupportedException($"Condition type not supported: {conditionType}");
            }
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
