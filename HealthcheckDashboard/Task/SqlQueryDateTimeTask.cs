using System;
using System.Threading.Tasks;
using HealthcheckDashboard.ResourceNS;

namespace HealthcheckDashboard.TaskNS
{
    // Executes a SQL query expected to return a single datetime (e.g. "select MAX(ValidFrom) as MaxValidFrom from LaidBom")
    class SqlQueryDateTimeTask : ITask
    {
        public string Name { get; }
        public ConnectionStringWithQueryResource Resource { get; }
        public DateTime LastResult { get; private set; }
        public override string ToString() => $"SqlQueryDateTimeTask(LastResult={LastResult})";

        public SqlQueryDateTimeTask(string name, ConnectionStringWithQueryResource resource)
        {
            Name = name ?? "(unnamed)";
            Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            LastResult = DateTime.MinValue;
        }

        // Performs the query and stores the resulting DateTime (if any) in LastResult.
        public async Task PerformAsync()
        {
            // Use System.Data.SqlClient to avoid extra package assumptions.
            using var conn = new Microsoft.Data.SqlClient.SqlConnection(Resource.ConnectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = Resource.Query;
            var scalar = await cmd.ExecuteScalarAsync();

            if (scalar == null || scalar == DBNull.Value)
            {
                // No result -> leave LastResult as DateTime.MinValue
                return;
            }

            // Try to convert to DateTime
            if (scalar is DateTime dt)
            {
                LastResult = dt;
                return;
            }

            // Attempt string parse fallback
            if (DateTime.TryParse(scalar.ToString(), out var parsed))
            {
                LastResult = parsed;
                return;
            }

            // If conversion fails, throw so Program will log the error
            throw new InvalidOperationException("SQL query did not return a DateTime convertible value.");
        }
    }
}