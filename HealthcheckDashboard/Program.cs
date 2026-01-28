using System;
using System.Collections.Concurrent;
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
        // ensure console color changes are atomic across threads
        private static readonly object ConsoleLock = new object();

        // store last condition evaluation per configured task (key = task instance id)
        private static readonly ConcurrentDictionary<int, bool?> LastConditionResults = new ConcurrentDictionary<int, bool?>();

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
            var taskInstanceId = 0;

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

                    // assign a stable id for this configured task instance
                    var myId = Interlocked.Increment(ref taskInstanceId) - 1;
                    LastConditionResults.TryAdd(myId, null);

                    // capture locals for closure
                    var localTaskName = taskName;
                    var localTask = task;
                    var localResource = resource;
                    var localCondition = condition;

                    // Start background runner for this configured task (runs immediately once, then according to schedule)
                    var bgTask = RunInBackground(schedule.TimeSpan, () =>
                    {
                        // run the configured task and evaluate condition if provided
                        try
                        {
                            localTask.Perform();

                            if (localTask is GetFileLastModifiedDateTask gf)
                            {
                                var value = gf.LastModifiedDate;
                                var result = localCondition != null ? localCondition.EvaluateCondition(value) : false;
                                var message = $"[{localTaskName}] Performed Task: {localTask}\nResource: {localResource}\nValue: {value}\nCondition: {localCondition}\nResult: {result}";

                                // determine whether a transition occurred that requires a warning
                                var prevOpt = LastConditionResults.TryGetValue(myId, out var prev) ? prev : null;
                                var warningNeeded = false;
                                if (localCondition != null && prevOpt.HasValue)
                                {
                                    switch (localCondition.WarnWhen)
                                    {
                                        case WarnWhen.becomesTrue:
                                            if (!prevOpt.Value && result) warningNeeded = true;
                                            break;
                                        case WarnWhen.becomesFalse:
                                            if (prevOpt.Value && !result) warningNeeded = true;
                                            break;
                                        case WarnWhen.changes:
                                            if (prevOpt.Value != result) warningNeeded = true;
                                            break;
                                    }
                                }

                                // update stored last result
                                LastConditionResults[myId] = result;

                                // write message; color red if result is false OR if a warning is required.
                                lock (ConsoleLock)
                                {
                                    var original = Console.ForegroundColor;
                                    var shouldColorRed = (localCondition != null && !result) || warningNeeded;
                                    if (shouldColorRed)
                                    {
                                        Console.ForegroundColor = ConsoleColor.Red;
                                    }

                                    // atomic write
                                    Console.WriteLine(message);

                                    if (warningNeeded)
                                    {
                                        Console.WriteLine("!!!! Warning !!!!");
                                    }

                                    Console.ForegroundColor = original;
                                }
                            }
                            else
                            {
                                // generic fallback
                                lock (ConsoleLock)
                                {
                                    Console.WriteLine($"[{localTaskName}] Performed Task: {localTask}\nResource: {localResource}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (ConsoleLock)
                            {
                                var original = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"[{localTaskName}] Task execution error: {ex}");
                                Console.ForegroundColor = original;
                            }
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

                    // parse optional warnWhen setting from JSON (default: becomesFalse)
                    WarnWhen warnWhen = WarnWhen.becomesFalse;
                    if (conditionElement.TryGetProperty("warnWhen", out var w) && w.ValueKind == JsonValueKind.String)
                    {
                        Enum.TryParse<WarnWhen>(w.GetString(), ignoreCase: true, out warnWhen);
                    }

                    return new DateTimeNotOlderThanTimeSpanCondition(TimeSpan.FromSeconds(notOlderSeconds), warnWhen);
                default:
                    throw new NotSupportedException($"Condition type not supported: {conditionType}");
            }
        }

        // Runs action immediately once (on a background thread) and then schedules subsequent runs using PeriodicTimer.
        // Returns a Task that represents the long-running background runner.
        public static Task RunInBackground(TimeSpan timeSpan, Action action)
        {
            return Task.Run(async () =>
            {
                // Initial immediate run
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    lock (ConsoleLock)
                    {
                        var original = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[RunInBackground] Initial run error: {ex}");
                        Console.ForegroundColor = original;
                    }
                }

                // Schedule subsequent runs
                using var periodicTimer = new PeriodicTimer(timeSpan);
                while (await periodicTimer.WaitForNextTickAsync())
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        lock (ConsoleLock)
                        {
                            var original = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[RunInBackground] Scheduled run error: {ex}");
                            Console.ForegroundColor = original;
                        }
                    }
                }
            });
        }
    }
}