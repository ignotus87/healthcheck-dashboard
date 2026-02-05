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
using System.Windows.Forms;

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
            try
            {
                ConsoleHelper.EnsureConsole();

                // start notifier early (harmless if already started)
                DesktopNotifier.Initialize();

                await ConfigureAndRun();

                await Console.Out.WriteLineAsync("FINISHED");

                // allow notifier to finish any queued notifications before exit
                DesktopNotifier.Shutdown();
            }
            finally
            {
                ConsoleHelper.ReleaseConsole();
            }
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
                    var task = CreateTask(taskConfig.GetProperty("name").GetString(),
                        taskConfig.GetProperty("taskType").GetString(), resource);

                    // Create schedule
                    var scheduleElement = taskConfig.GetProperty("schedule");
                    var intervalSeconds = scheduleElement.TryGetProperty("intervalSeconds", out var s) ? s.GetInt32() : 60;
                    var schedule = new Schedule(TimeSpan.FromSeconds(intervalSeconds));

                    // Create condition (optional)
                    ICondition condition = null;
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
                    bool? conditionResult = null;
                    string message;

                    // Start background runner for this configured task (runs immediately once, then according to schedule)
                    var bgTask = RunInBackground(schedule.TimeSpan, async () =>
                    {
                        // run the configured task and evaluate condition if provided
                        try
                        {
                            await localTask.PerformAsync();

                            bool foundTask = false;

                            if (localTask is GetFileLastModifiedDateTask gf)
                            {
                                foundTask = true;
                                var value = gf.LastModifiedDate;
                                conditionResult = localCondition != null ? localCondition.EvaluateCondition(value) : false;
                            }
                            else if (localTask is MakeWebRequestTask requestTask)
                            {
                                foundTask = true;
                                conditionResult = localCondition != null ? localCondition.EvaluateCondition(requestTask.LastResult) : false;
                            }
                            else if (localTask is SqlQueryDateTimeTask sqlTask)
                            {
                                foundTask = true;
                                var value = sqlTask.LastResult;
                                conditionResult = localCondition != null ? localCondition.EvaluateCondition(value) : false;
                            }
                            else if (localTask is SqlQueryIntTask sqlIntTask)
                            {
                                foundTask = true;
                                var value = sqlIntTask.LastResult;
                                conditionResult = localCondition != null ? localCondition.EvaluateCondition(value) : false;
                            }

                            if (foundTask)
                            {
                                message = Environment.NewLine + $"[{localTaskName}] Performed Task: {localTask}\n=> {localCondition}";
                            }
                            else
                            {
                                // generic fallback
                                message = "Fell back to the generic fallback implementation." + $"[{localTaskName}] Performed Task: {localTask}\nResource: {localResource}";
                            }

                            // determine whether a transition occurred that requires a warning
                            var prevOpt = LastConditionResults.TryGetValue(myId, out var prev) ? prev : null;
                            var warningNeeded = false;
                            if (localCondition != null && prevOpt.HasValue && conditionResult.HasValue)
                            {
                                warningNeeded = prevOpt.Value != conditionResult.Value;
                            }

                            // update stored last result
                            LastConditionResults[myId] = conditionResult;

                            // write message; color red if result is false OR if a warning is required.
                            lock (ConsoleLock)
                            {
                                var original = Console.ForegroundColor;
                                var shouldColorRed = (localCondition != null && conditionResult.HasValue && conditionResult.Value == localCondition.WarnWhen) || warningNeeded;
                                Console.ForegroundColor = shouldColorRed ? ConsoleColor.Red : ConsoleColor.Green;

                                // atomic write
                                Console.WriteLine(message);

                                if (warningNeeded)
                                {
                                    Console.WriteLine("!!!! Warning !!!! Value changed !!!");
                                }

                                Console.ForegroundColor = original;

                                // Show desktop notification if result is red or a warning transition occurred
                                if (shouldColorRed)
                                {
                                    try
                                    {
                                        var title = $"Healthcheck: {localTaskName}";
                                        var body = localCondition != null ? localCondition.ToString() : "Condition triggered";
                                        var icon = warningNeeded ? ToolTipIcon.Warning : ToolTipIcon.Error;
                                        DesktopNotifier.Notify(title, body, icon, 5000);
                                    }
                                    catch
                                    {
                                        // Do not fail the background runner on notification failure
                                    }
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
                case "UrlResource":
                    var url = resourceElement.GetProperty("url").GetString();
                    return new UrlResource(url);
                case "ConnectionStringWithQueryResource":
                    var cs = resourceElement.GetProperty("connectionString").GetString();
                    var query = resourceElement.GetProperty("query").GetString();
                    return new ConnectionStringWithQueryResource(cs, query);
                default:
                    throw new NotSupportedException($"Resource type not supported: {resourceType}");
            }
        }

        private static ITask CreateTask(string taskName, string taskType, Resource resource)
        {
            switch (taskType)
            {
                case "GetFileLastModifiedDateTask":
                    if (resource is GeneralFileResource gfr)
                        return new GetFileLastModifiedDateTask(taskName, gfr);
                    throw new ArgumentException("GetFileLastModifiedDateTask requires a GeneralFileResource");
                case "MakeWebRequestTask":
                    if (resource is UrlResource ur)
                        return new MakeWebRequestTask(taskName, ur);
                    throw new ArgumentException("MakeWebRequestTask requires a UrlResource");
                case "SqlQueryDateTimeTask":
                    {
                        if (resource is ConnectionStringWithQueryResource csq)
                            return new SqlQueryDateTimeTask(taskName, csq);
                        throw new ArgumentException("SqlQueryDateTimeTask requires a ConnectionStringWithQueryResource resource");
                    }
                case "SqlQueryIntTask":
                    {
                        if (resource is ConnectionStringWithQueryResource csq)
                            return new SqlQueryIntTask(taskName, csq);
                        throw new ArgumentException("SqlQueryIntTask requires a ConnectionStringWithQueryResource resource");
                    }
                default:
                    throw new NotSupportedException($"Task type not supported: {taskType}");
            }
        }

        private static ICondition CreateCondition(JsonElement conditionElement)
        {
            var conditionType = conditionElement.GetProperty("conditionType").GetString();
            bool warnWhen;

            // parse warnWhen setting from JSON
            {
                if (conditionElement.TryGetProperty("warnWhen", out var wt) && wt.ValueKind == JsonValueKind.True)
                {
                    warnWhen = true;
                }
                else if (conditionElement.TryGetProperty("warnWhen", out var wf) && wf.ValueKind == JsonValueKind.False)
                {
                    warnWhen = false;
                }
                else
                {
                    throw new ConfigurationException("WarnWhen setting is required for all tasks!");
                }
            }

            switch (conditionType)
            {
                case "DateTimeNotOlderThanTimeSpanCondition":
                    var notOlderSeconds = conditionElement.TryGetProperty("notOlderThanSeconds", out var s) ? s.GetInt32() : 60;

                    return new DateTimeNotOlderThanTimeSpanCondition(TimeSpan.FromSeconds(notOlderSeconds), warnWhen);

                case "ContentIsDifferentCondition":

                    var contentFilePath = conditionElement.TryGetProperty("contentFilePath", out var cfp) && cfp.ValueKind == JsonValueKind.String
                        ? cfp.GetString()
                        : null;

                    return new ContentIsDifferentCondition(contentFilePath, warnWhen);

                case "SqlQueryResultIsOlderThanCondition":
                    var limitSeconds = conditionElement.TryGetProperty("limitSeconds", out var l) ? l.GetInt32() : 60;
                    return new SqlQueryResultIsOlderThanCondition(TimeSpan.FromSeconds(limitSeconds), warnWhen);

                case "SqlQueryIntResultIsGreaterThanCondition":
                    int valueInCondition = conditionElement.TryGetProperty("value", out var v) ? v.GetInt32() : 0;
                    return new SqlQueryIntResultIsGreaterThanCondition(valueInCondition, warnWhen);

                default:
                    throw new NotSupportedException($"Condition type not supported: {conditionType}");
            }
        }

        // Runs action immediately once (on a background thread) and then schedules subsequent runs using PeriodicTimer.
        // Returns a Task that represents the long-running background runner.
        public static Task RunInBackground(TimeSpan timeSpan, Func<Task> action)
        {
            return Task.Run(async () =>
            {
                // Initial immediate run
                try
                {
                    await action();
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
                        await action();
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