using System;
using System.Threading.Tasks;
using HealthcheckDashboard.ResourceNS;

namespace HealthcheckDashboard.TaskNS
{
    // Executes a SQL query expected to return a single int (e.g. "select COUNT(*) as NumberOrRows from Whatever")
    class SqlQueryIntTask : ITask
    {
        public string Name { get; }
        public ConnectionStringWithQueryResource Resource { get; }
        public int LastResult { get; private set; }
        public override string ToString() => $"SqlQueryIntTask(LastResult={LastResult})";

        public SqlQueryIntTask(string name, ConnectionStringWithQueryResource resource)
        {
            Name = name ?? "(unnamed)";
            Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            LastResult = int.MinValue;
        }

        // Performs the query and stores the resulting int (if any) in LastResult.
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
                // No result -> leave LastResult as int.MinValue
                return;
            }

            // Try to convert to int
            if (scalar is int number)
            {
                LastResult = number;
                return;
            }

            // Attempt string parse fallback
            if (int.TryParse(scalar.ToString(), out var parsed))
            {
                LastResult = parsed;
                return;
            }

            // If conversion fails, throw so Program will log the error
            throw new InvalidOperationException("SQL query did not return an int convertible value.");
        }
    }
}