using System;

namespace HealthcheckDashboard.ResourceNS
{
    // Simple resource holder for SQL connection string + query
    class ConnectionStringResource : Resource
    {
        public string ConnectionString { get; }
        public string Query { get; }

        public ConnectionStringResource(string connectionString)
            : base(ResourceType.ConnectionStringWithQuery)
        {
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public override string ToString() => $"ConnectionStringResource:{ConnectionString}";
    }
}