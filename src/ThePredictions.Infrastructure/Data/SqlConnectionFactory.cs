using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;
using System.Data;

namespace ThePredictions.Infrastructure.Data;

[ExcludeFromCodeCoverage(Justification = "Database plumbing: connection, transaction and type-handler wiring with no branching logic of its own.")]
public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration, IOptions<TimeoutSettings> timeoutSettings)
    {
        var rawConnectionString = configuration.GetConnectionString("DataConnection")
            ?? throw new InvalidOperationException("Connection string 'DataConnection' not found.");

        var builder = new SqlConnectionStringBuilder(rawConnectionString)
        {
            CommandTimeout = timeoutSettings.Value.DatabaseCommandTimeoutSeconds,
            MinPoolSize = 5,
            MaxPoolSize = 100,
            ConnectRetryCount = 3,
            ConnectRetryInterval = 10,
            LoadBalanceTimeout = 300
        };

        _connectionString = builder.ConnectionString;
    }

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
