using ThePredictions.Application.Features.Account.Queries;
using ThePredictions.Application.Features.Homepage.Queries;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Features.Onboarding.Queries;
using ThePredictions.Application.Features.Prizes.Queries;
using ThePredictions.Persistence.Conformance;
using ThePredictions.Persistence.Conformance.Queries;
using ThePredictions.Persistence.SqlServer.Queries.Account;
using ThePredictions.Persistence.SqlServer.Queries.Homepage;
using ThePredictions.Persistence.SqlServer.Queries.Leagues;
using ThePredictions.Persistence.SqlServer.Queries.Onboarding;
using ThePredictions.Persistence.SqlServer.Queries.Prizes;
using ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Queries;

/// <summary>Runs <see cref="PlayerPagesQueryConformanceTests"/> against SQL Server.</summary>
[Collection(DatabaseCollection.Name)]
[Trait(IntegrationTrait.Name, IntegrationTrait.Value)]
public class SqlServerPlayerPagesQueryTests(SqlServerDatabaseFixture fixture)
    : PlayerPagesQueryConformanceTests, IAsyncLifetime
{
    private readonly SqlServerTestHarness _harness = new(fixture);

    protected override IHomepageSeasonsQuery HomepageSeasons => new HomepageSeasonsQuery(_harness.ReadDbConnection);

    protected override IAccountProfileQuery AccountProfile => new AccountProfileQuery(_harness.ReadDbConnection);

    protected override IMyPayoutDetailsQuery MyPayoutDetails => new MyPayoutDetailsQuery(_harness.ReadDbConnection);

    protected override IOnboardingStateQuery OnboardingState => new OnboardingStateQuery(_harness.ReadDbConnection);

    protected override IManageLeaguesQuery ManageLeagues => new ManageLeaguesQuery(_harness.ReadDbConnection);

    protected override ILeagueBankDetailsQuery LeagueBankDetails => new LeagueBankDetailsQuery(_harness.ReadDbConnection);

    protected override ILeagueEmailRecipientQuery LeagueEmailRecipient => new LeagueEmailRecipientQuery(_harness.ReadDbConnection);

    protected override IPrizeSchemeSeasonQuery PrizeSchemeSeason => new PrizeSchemeSeasonQuery(_harness.ReadDbConnection);

    protected override ITestDataSeeder Seed => _harness.Seed;

    public ValueTask InitializeAsync() => _harness.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
