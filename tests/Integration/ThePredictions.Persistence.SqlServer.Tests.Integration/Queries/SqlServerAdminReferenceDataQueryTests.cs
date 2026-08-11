using ThePredictions.Application.Features.Admin.Competitions.Queries;
using ThePredictions.Application.Features.Admin.EmailTests.Queries;
using ThePredictions.Application.Features.Admin.PricingSettings.Queries;
using ThePredictions.Application.Features.Admin.RunningCosts.Queries;
using ThePredictions.Application.Features.Admin.ServiceFees.Queries;
using ThePredictions.Application.Features.Admin.Teams.Queries;
using ThePredictions.Persistence.Conformance;
using ThePredictions.Persistence.Conformance.Queries;
using ThePredictions.Persistence.SqlServer.Queries.Admin.Competitions;
using ThePredictions.Persistence.SqlServer.Queries.Admin.EmailTests;
using ThePredictions.Persistence.SqlServer.Queries.Admin.Settings;
using ThePredictions.Persistence.SqlServer.Queries.Admin.Teams;
using ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Queries;

/// <summary>Runs <see cref="AdminReferenceDataQueryConformanceTests"/> against SQL Server.</summary>
[Collection(DatabaseCollection.Name)]
[Trait(IntegrationTrait.Name, IntegrationTrait.Value)]
public class SqlServerAdminReferenceDataQueryTests(SqlServerDatabaseFixture fixture)
    : AdminReferenceDataQueryConformanceTests, IAsyncLifetime
{
    private readonly SqlServerTestHarness _harness = new(fixture);

    protected override ICompetitionsQuery Competitions => new CompetitionsQuery(_harness.ReadDbConnection);

    protected override ITeamsQuery Teams => new TeamsQuery(_harness.ReadDbConnection);

    protected override ISeasonTeamsQuery SeasonTeams => new SeasonTeamsQuery(_harness.ReadDbConnection);

    protected override IPricingSettingsQuery PricingSettings => new PricingSettingsQuery(_harness.ReadDbConnection);

    protected override IRunningCostsQuery RunningCosts => new RunningCostsQuery(_harness.ReadDbConnection);

    protected override IServiceFeesQuery ServiceFees => new ServiceFeesQuery(_harness.ReadDbConnection);

    protected override IEmailTestUserQuery EmailTestUser => new EmailTestUserQuery(_harness.ReadDbConnection);

    protected override ITestDataSeeder Seed => _harness.Seed;

    public ValueTask InitializeAsync() => _harness.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
