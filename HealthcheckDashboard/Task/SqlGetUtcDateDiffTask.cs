using System;
using System.Threading.Tasks;
using HealthcheckDashboard.ResourceNS;

namespace HealthcheckDashboard.TaskNS
{
    // Executes a SQL query expected to return a single datetime (e.g. "select MAX(ValidFrom) as MaxValidFrom from LaidBom")
    class SqlGetUtcDateDiffTask : ITask
    {
        public string Name { get; }
        public bool IsEnabled { get; } = true;
        public ConnectionStringResource Resource { get; }
        public DateTime SentAtUtc { get; private set; } = DateTime.MinValue;
        public DateTime ReceivedAtUtc { get; private set; } = DateTime.MinValue;
        public TimeSpan LastResult { get; private set; }
        public override string ToString() => $"SqlQueryDateTimeTask(LastResult={LastResult})";

        public SqlGetUtcDateDiffTask(string name, ConnectionStringResource resource)
        {
            Name = name ?? "(unnamed)";
            Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            LastResult = TimeSpan.MaxValue;
        }

        // Performs the query and stores the resulting DateTime (if any) in LastResult.
        public async Task PerformAsync()
        {
            // Use System.Data.SqlClient to avoid extra package assumptions.
            using var conn = new Microsoft.Data.SqlClient.SqlConnection(Resource.ConnectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT GETUTCDATE() as UtcDateAtServer";

            SentAtUtc = DateTime.UtcNow;

            var scalar = await cmd.ExecuteScalarAsync();

            ReceivedAtUtc = DateTime.UtcNow;

            if (scalar == null || scalar == DBNull.Value)
            {
                // No result -> leave LastResult as its preset value (TimeSpan.MaxValue to indicate error)
                return;
            }

            // Try to convert to DateTime
            if (scalar is DateTime dt)
            {
                var dateTimeUtcFromServer = dt;
                var delayFromQueryToReceive = (ReceivedAtUtc - SentAtUtc) / 2.0;
                LastResult = dateTimeUtcFromServer + delayFromQueryToReceive - DateTime.UtcNow;
                return;
            }

            // Attempt string parse fallback
            if (DateTime.TryParse(scalar.ToString(), out var parsed))
            {
                var dateTimeUtcFromServer = parsed;
                var delayFromQueryToReceive = (ReceivedAtUtc - SentAtUtc) / 2.0;
                LastResult = dateTimeUtcFromServer + delayFromQueryToReceive - DateTime.UtcNow;
                return;
            }

            // If conversion fails, throw so Program will log the error
            throw new InvalidOperationException("SQL query did not return a DateTime convertible value.");
        }
    }
}