using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThePredictions.Application.Data;
using ThePredictions.Persistence.SqlServer.Data;
using ThePredictions.Persistence.SqlServer.Data.Resilience;

namespace ThePredictions.Persistence.SqlServer;

/// <summary>
/// Registers the SQL Server persistence adapter. Kept separate from
/// <c>AddInfrastructureServices</c> so the choice of database is one call in the composition root
/// rather than something tangled through the registration of Brevo, Stripe and the football API.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Container registration: verified by ThePredictions.Composition.Tests.Unit, which resolves every handler from the real container.")]
public static class DependencyInjection
{
    public static void AddSqlServerPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SqlRetryPolicyOptions>(
            configuration.GetSection(SqlRetryPolicyOptions.SectionName));
        services.AddSingleton<ISqlRetryPolicy, SqlRetryPolicy>();

        services.AddScoped<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IDbTransactionContext, DbTransactionContext>();
        services.AddScoped<IApplicationReadDbConnection, DapperReadDbConnection>();

        var connectionString = configuration.GetConnectionString("DataConnection")
                               ?? throw new InvalidOperationException("Connection string 'DataConnection' not found.");

        // The database probe moves with the adapter: a different adapter would probe differently, and
        // AddHealthChecks() composes, so the football-api check registered by Infrastructure still lands
        // in the same registry.
        services.AddHealthChecks()
            .AddSqlServer(connectionString, name: "database", tags: ["ready"]);
    }
}
