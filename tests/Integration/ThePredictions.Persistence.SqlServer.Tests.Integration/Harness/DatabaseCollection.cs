using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;

/// <summary>
/// Every test in this assembly joins this collection, which is what makes the container shared and the
/// tests sequential. Both matter: starting a SQL Server per class would dominate the run time, and
/// Respawn wipes the whole database between tests, so two tests running at once would delete each
/// other's arrangement.
/// </summary>
[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<SqlServerDatabaseFixture>
{
    public const string Name = "SQL Server database";
}
