using System;

namespace HealthcheckDashboard.ResourceNS
{
    // Simple resource holder for SQL connection string + query
    class ConnectionStringWithQueryResource : Resource
    {
        public string ConnectionString { get; }
        public string Query { get; }

        public ConnectionStringWithQueryResource(string connectionString, string query)
            : base(ResourceType.ConnectionStringWithQuery)
        {
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            Query = query ?? throw new ArgumentNullException(nameof(query));
        }

        public override string ToString() => $"ConnectionStringWithQueryResource: Query=\"{Query}\"";
    }
}