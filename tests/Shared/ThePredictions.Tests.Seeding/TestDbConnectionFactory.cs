using System.Data;
using Microsoft.Data.SqlClient;
using ThePredictions.Application.Data;

namespace ThePredictions.Tests.Seeding;

/// <summary>
/// The application's connection seam, pointed at the test container. Deliberately not
/// <c>SqlConnectionFactory</c>: that one reads <c>ConnectionStrings:DataConnection</c> from
/// <c>IConfiguration</c> and rebuilds the string with production pool sizes and connect-retry
/// settings, none of which a single-test connection wants.
/// </summary>
public sealed class TestDbConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public IDbConnection CreateConnection() => new SqlConnection(connectionString);
}
