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
using System.Linq;
using HealthcheckDashboard.ViewModel;

namespace HealthcheckDashboard
{
    class Program
    {
        private static TrayIconManager _trayIconManager;

        // ensure console color changes are atomic across threads
        private static readonly object ConsoleLock = new object();

        // store last condition evaluation per configured task (key = task instance id)
        private static readonly ConcurrentDictionary<int, bool?> LastConditionResults = new ConcurrentDictionary<int, bool?>();

        public static List<TaskItemViewModel> TaskItems { get; } = new List<TaskItemViewModel>();

        static async Task Main(string[] args)
        {
            bool initializationSuccess = false;

            try
            {
                ConsoleHelper.EnsureConsole();
                DesktopNotifier.Initialize();

                // Initialize tray icon
                _trayIconManager = new TrayIconManager();

                // Hide console after 5 seconds
                await Console.Out.WriteLineAsync("*** Hiding the console window after 5 seconds ***");
                _ = Task.Delay(5000).ContinueWith(_ => ConsoleHelper.HideConsole());

                try
                {
                    initializationSuccess = await ConfigureAndRun();
                }
                catch
                {
                }
                finally
                {
                    if (!initializationSuccess)
                    {
                        await Console.Out.WriteLineAsync("Preass any key to exit");
                        Console.ReadKey();
                    }

                    await Console.Out.WriteLineAsync("FINISHED");
                    DesktopNotifier.Shutdown();
                }

                
            }
            finally
            {
                await Console.Out.WriteLineAsync("*** Closing the app after 5 seconds ***");
                _trayIconManager?.Dispose();
                ConsoleHelper.ReleaseConsole();
            }
        }

        public static async Task<bool> ConfigureAndRun()
        {
            await Console.Out.WriteLineAsync("CONFIGURE");

            // locate configuration file (next to the running executable)
            var configPath = Path.Combine(AppContext.BaseDirectory, "tasks.json");
            if (!File.Exists(configPath))
            {
                await Console.Error.WriteLineAsync($"Configuration file not found: {configPath}");
                return false;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(File.ReadAllText(configPath));
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Failed to parse configuration: {ex.Message}");
                return false;
            }

            if (!doc.RootElement.TryGetProperty("tasks", out var tasksElement) || tasksElement.ValueKind != JsonValueKind.Array)
            {
                await Console.Error.WriteLineAsync("Configuration does not contain a 'tasks' array.");
                return false;
            }

            var backgroundTasks = new List<Task>();
            var taskInstanceId = 0;

            foreach (var taskConfig in tasksElement.EnumerateArray())
            {
                try
                {
                    var taskName = taskConfig.TryGetProperty("name", out var n) ? n.GetString() : "(unnamed)";
                    bool isEnabled = taskConfig.TryGetProperty("isEnabled", out var e) && e.ValueKind == JsonValueKind.False ? false : true;

                    if (!isEnabled)
                    {
                        await Console.Out.WriteLineAsync($"Skipping disabled task: {taskName}");
                        continue;
                    }

                    // Create resource
                    var resource = CreateResource(taskConfig.GetProperty("resource"));

                    // Create task
                    var task = CreateTask(taskConfig.GetProperty("name").GetString(),
                        taskConfig.GetProperty("taskType").GetString(), resource, taskConfig);

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

                    TaskItems.Add(new TaskItemViewModel
                    {
                        Task = task,
                        Resource = resource,
                        Condition = condition
                    });

                    // Start background runner for this configured task (runs immediately once, then according to schedule)
                    var bgTask = RunInBackground(schedule.TimeSpan, async () =>
                    {
                        var myTask = TaskItems.Single(x => x.Task.Name == localTask.Name);

                        // run the configured task and evaluate condition if provided
                        try
                        {
                            myTask.NextRunTime = DateTime.Now.Add(schedule.TimeSpan);

                            await ExecuteTaskNow(myTask, myId);
                        }
                        catch (Exception ex)
                        {
                            lock (ConsoleLock)
                            {
                                var original = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"[{localTaskName}] Task execution error: {ex}");
                                myTask.Messages.Add($"[{localTaskName}] Task execution error: {ex}");
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
            return true;
        }

        public static async Task ExecuteTaskNow(TaskItemViewModel taskItem, int taskInstanceId)
        {
            if (taskItem?.Task == null) return;

            var localTask = taskItem.Task;
            var localTaskName = taskItem.Task.Name;
            var localCondition = taskItem.Condition;
            var localResource = taskItem.Resource;
            bool? conditionResult = null;
            string message;

            try
            {
                taskItem.LastRunTime = DateTime.Now;

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
                else if (localTask is SqlGetUtcDateDiffTask sqlGetUtcDateDiffTask)
                {
                    foundTask = true;
                    var value = (int)Math.Abs(sqlGetUtcDateDiffTask.LastResult.TotalMilliseconds);
                    conditionResult = localCondition != null ? localCondition.EvaluateCondition(value) : false;
                }
                else if (localTask is SqlQueryIntTask sqlIntTask)
                {
                    foundTask = true;
                    var value = sqlIntTask.LastResult;
                    conditionResult = localCondition != null ? localCondition.EvaluateCondition(value) : false;
                }
                else if (localTask is FindLinesInLatestFileContainingErrorTask findErrorTask)
                {
                    foundTask = true;
                    var value = findErrorTask.LineWithError;
                    conditionResult = localCondition != null ? localCondition.EvaluateCondition(value) : false;
                }

                if (foundTask)
                {
                    message = Environment.NewLine + $"[{localTaskName}] Performed Task: {localTask}\n=> {localCondition}";
                    taskItem.Messages.Add(message);
                }
                else
                {
                    // generic fallback
                    message = "Fell back to the generic fallback implementation." + $"[{localTaskName}] Performed Task: {localTask}\nResource: {localResource}";
                    taskItem.Messages.Add(message);
                }

                // determine whether a transition occurred that requires a warning
                var prevOpt = LastConditionResults.TryGetValue(taskInstanceId, out var prev) ? prev : null;
                var hasValueChanged = false;
                if (localCondition != null && prevOpt.HasValue && conditionResult.HasValue)
                {
                    hasValueChanged = prevOpt.Value != conditionResult.Value;
                }

                // update stored last result
                LastConditionResults[taskInstanceId] = conditionResult;
                taskItem.LastResult = conditionResult;

                // write message; color red if result is false OR if a warning is required.
                lock (ConsoleLock)
                {
                    var original = Console.ForegroundColor;
                    var shouldColorRed = (localCondition != null && conditionResult.HasValue && conditionResult.Value == localCondition.WarnWhen);
                    Console.ForegroundColor = shouldColorRed ? ConsoleColor.Red : ConsoleColor.Green;
                    DesktopNotifier.UpdateTaskbarIcon(shouldColorRed ? DesktopNotifier.TaskState.Error : DesktopNotifier.TaskState.Success);

                    // atomic write
                    Console.WriteLine(message);

                    if (hasValueChanged)
                    {
                        Console.WriteLine("Info: Value changed");
                    }

                    Console.ForegroundColor = original;

                    // Show desktop notification if result is red or a warning transition occurred
                    if (shouldColorRed)
                    {
                        try
                        {
                            var title = $"Healthcheck: {localTaskName}";
                            var body = (localTask.ToString() ?? "") + "|" + (localCondition != null ? localCondition.ToString() : "Condition triggered");
                            var icon = shouldColorRed ? (hasValueChanged ? ToolTipIcon.Warning : ToolTipIcon.Error) : ToolTipIcon.Info;
                            DesktopNotifier.Notify(title, body, icon, 60000);
                        }
                        catch
                        {
                            // Do not fail on notification failure
                        }
                    }
                    else
                    {
                        // Show green notification and close it 10 seconds later
                        var title = $"OK: {localTaskName}";
                        var body = (localTask.ToString() ?? "") + "|" + (localCondition != null ? localCondition.ToString() : "Condition triggered");
                        var icon = ToolTipIcon.Info;
                        DesktopNotifier.Notify(title, body, icon, 5000);
                    }

                    taskItem.Messages.Add(localCondition?.ToString() ?? localTask.ToString());
                }
            }
            catch (Exception ex)
            {
                lock (ConsoleLock)
                {
                    var original = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[{localTaskName}] Task execution error: {ex}");
                    taskItem.Messages.Add($"[{localTaskName}] Task execution error: {ex}");
                    Console.ForegroundColor = original;
                }
            }
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
                case nameof(ConnectionStringWithQueryResource):
                    {
                        var cs = resourceElement.GetProperty("connectionString").GetString();
                        var query = resourceElement.GetProperty("query").GetString();
                        return new ConnectionStringWithQueryResource(cs, query);
                    }
                case nameof(ConnectionStringResource):
                    {
                        var cs = resourceElement.GetProperty("connectionString").GetString();
                        return new ConnectionStringResource(cs);
                    }
                case "LatestFileResource":
                    var fileSearchPath = resourceElement.GetProperty("fileSearchPath").GetString();
                    return new LatestFileResource(fileSearchPath);
                default:
                    throw new NotSupportedException($"Resource type not supported: {resourceType}");
            }
        }

        private static ITask CreateTask(string taskName, string taskType, Resource resource, JsonElement taskConfigJsonElement)
        {
            switch (taskType)
            {
                case nameof(GetFileLastModifiedDateTask):
                    if (resource is GeneralFileResource gfr)
                        return new GetFileLastModifiedDateTask(taskName, gfr);
                    throw new ArgumentException("GetFileLastModifiedDateTask requires a GeneralFileResource");
                case nameof(MakeWebRequestTask):
                    if (resource is UrlResource ur)
                        return new MakeWebRequestTask(taskName, ur);
                    throw new ArgumentException("MakeWebRequestTask requires a UrlResource");
                case nameof(SqlQueryDateTimeTask):
                    {
                        if (resource is ConnectionStringWithQueryResource csq)
                            return new SqlQueryDateTimeTask(taskName, csq);
                        throw new ArgumentException("SqlQueryDateTimeTask requires a ConnectionStringWithQueryResource resource");
                    }
                case nameof(SqlQueryIntTask):
                    {
                        if (resource is ConnectionStringWithQueryResource csq)
                            return new SqlQueryIntTask(taskName, csq);
                        throw new ArgumentException("SqlQueryIntTask requires a ConnectionStringWithQueryResource resource");
                    }
                case nameof(SqlGetUtcDateDiffTask):
                    {
                        if (resource is ConnectionStringResource csr)
                            return new SqlGetUtcDateDiffTask(taskName, csr);
                        throw new ArgumentException($"{nameof(SqlGetUtcDateDiffTask)} requires a {nameof(ConnectionStringResource)} resource");
                    }
                case nameof(FindLinesInLatestFileContainingErrorTask):
                    if (resource is LatestFileResource lfr)
                    {
                        string[] textPartsIndicatingError = taskConfigJsonElement.TryGetProperty("textPartsIndicatingError", out var cfp) && cfp.ValueKind == JsonValueKind.Array
                        ? cfp.EnumerateArray().ToArray().Select(x => x.ToString()).ToArray() : new string[] { };
                        string[] textPartsToExclude = taskConfigJsonElement.TryGetProperty("textPartsToExclude", out var cfp2) && cfp.ValueKind == JsonValueKind.Array
                        ? cfp2.EnumerateArray().ToArray().Select(x => x.ToString()).ToArray() : new string[] { };

                        return new FindLinesInLatestFileContainingErrorTask(taskName, lfr, textPartsIndicatingError, textPartsToExclude);
                    }
                    throw new ArgumentException("FindLinesInLatestFileContainingErrorTask requires a LatestFileResource");
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
                case nameof(DateTimeNotOlderThanTimeSpanCondition):
                    var notOlderSeconds = conditionElement.TryGetProperty("notOlderThanSeconds", out var s) ? s.GetInt32() : 60;

                    return new DateTimeNotOlderThanTimeSpanCondition(TimeSpan.FromSeconds(notOlderSeconds), warnWhen);

                case nameof(ContentIsDifferentCondition):
                    var contentFilePath = conditionElement.TryGetProperty("contentFilePath", out var cfp) && cfp.ValueKind == JsonValueKind.String
                        ? cfp.GetString()
                        : null;
                    return new ContentIsDifferentCondition(contentFilePath, warnWhen);

                case nameof(SqlQueryResultIsOlderThanCondition):
                    var limitSeconds = conditionElement.TryGetProperty("limitSeconds", out var l) ? l.GetInt32() : 60;
                    return new SqlQueryResultIsOlderThanCondition(TimeSpan.FromSeconds(limitSeconds), warnWhen);

                case nameof(SqlQueryIntResultIsGreaterThanCondition):
                    {
                        int valueInCondition = conditionElement.TryGetProperty("value", out var v) ? v.GetInt32() : 0;
                        return new SqlQueryIntResultIsGreaterThanCondition(valueInCondition, warnWhen);
                    }

                case nameof(SqlQueryDateTimeResultDiffIsGreaterThanMillisCondition):
                    {
                        int valueInCondition = conditionElement.TryGetProperty("limitMilliseconds", out var v) ? v.GetInt32() : 0;
                        return new SqlQueryDateTimeResultDiffIsGreaterThanMillisCondition(TimeSpan.FromMilliseconds(valueInCondition), warnWhen);
                    }

                case nameof(IntIsGreaterThanCondition):
                    {
                        int valueInCondition = conditionElement.TryGetProperty("value", out var v) ? v.GetInt32() : 0;
                        return new IntIsGreaterThanCondition(valueInCondition, warnWhen);
                    }

                case nameof(StringNotNullCondition):
                    return new StringNotNullCondition(warnWhen);

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