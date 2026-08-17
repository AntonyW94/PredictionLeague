using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThePredictions.Application.Features.Account.Queries;
using ThePredictions.Application.Features.Homepage.Queries;
using ThePredictions.Application.Features.Onboarding.Queries;
using ThePredictions.Application.Features.Prizes.Queries;
using ThePredictions.Persistence.SqlServer.Queries.Account;
using ThePredictions.Persistence.SqlServer.Queries.Homepage;
using ThePredictions.Persistence.SqlServer.Queries.Onboarding;
using ThePredictions.Persistence.SqlServer.Queries.Prizes;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Features.External.Tasks.Queries;
using ThePredictions.Persistence.SqlServer.Queries.External;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.Competitions.Queries;
using ThePredictions.Application.Features.Admin.EmailTests.Queries;
using ThePredictions.Application.Features.Admin.PricingSettings.Queries;
using ThePredictions.Application.Features.Admin.RunningCosts.Queries;
using ThePredictions.Application.Features.Admin.ServiceFees.Queries;
using ThePredictions.Application.Features.Admin.Teams.Queries;
using ThePredictions.Persistence.SqlServer.Queries.Admin.Competitions;
using ThePredictions.Persistence.SqlServer.Queries.Admin.EmailTests;
using ThePredictions.Persistence.SqlServer.Queries.Admin.Seasons;
using ThePredictions.Persistence.SqlServer.Queries.Admin.Settings;
using ThePredictions.Persistence.SqlServer.Queries.Admin.Teams;
using ThePredictions.Persistence.SqlServer.Queries.Admin.Users;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Application.Features.Admin.Seasons.Queries;
using ThePredictions.Application.Features.Admin.Users.Queries;
using ThePredictions.Application.Features.Badges.Queries;
using ThePredictions.Application.Features.Boosts.Queries;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Features.Predictions.Queries;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Application.Features.Sharing.Queries;
using ThePredictions.Application.Features.SeasonPasses.Queries;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Models;
using ThePredictions.Persistence.SqlServer.Identity;
using ThePredictions.Persistence.SqlServer.Data;
using ThePredictions.Persistence.SqlServer.Data.Resilience;
using ThePredictions.Persistence.SqlServer.Queries.Admin.Rounds;
using ThePredictions.Persistence.SqlServer.Queries.Badges;
using ThePredictions.Persistence.SqlServer.Queries.Boosts;
using ThePredictions.Persistence.SqlServer.Queries;
using ThePredictions.Persistence.SqlServer.Queries.Dashboard;
using ThePredictions.Persistence.SqlServer.Queries.Leagues;
using ThePredictions.Persistence.SqlServer.Queries.Pricing;
using ThePredictions.Persistence.SqlServer.Queries.Predictions;
using ThePredictions.Persistence.SqlServer.Queries.Rounds;
using ThePredictions.Persistence.SqlServer.Queries.Sharing;
using ThePredictions.Persistence.SqlServer.Queries.SeasonPasses;
using ThePredictions.Persistence.SqlServer.Repositories;

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

        AddIdentityStores(services);
        AddRepositories(services);
        AddQueries(services);
    }

    // ASP.NET Identity resolves its stores from the container, so registering the interfaces directly is
    // exactly what IdentityBuilder.AddUserStore/AddRoleStore do - and it lets the stores live with the
    // adapter while Infrastructure keeps the half of AddIdentity that is not persistence (password
    // policy, lockout, the sign-in manager, token providers). Order between the two calls does not
    // matter: AddIdentity registers no store of its own.
    private static void AddIdentityStores(IServiceCollection services)
    {
        services.AddScoped<IUserStore<ApplicationUser>, DapperUserStore>();
        services.AddScoped<IRoleStore<IdentityRole>, DapperRoleStore>();
    }

    // One registration per I*Query port in Application. Grows as the persistence split moves each
    // feature area's reads out of its handlers; a missing one is caught by
    // ThePredictions.Composition.Tests.Unit, which resolves every handler from the real container.
    private static void AddQueries(IServiceCollection services)
    {
        services.AddScoped<IBoostCatalogueQuery, BoostCatalogueQuery>();
        services.AddScoped<ILeagueBoostUsageQuery, LeagueBoostUsageQuery>();
        services.AddScoped<IRoundCompletionQuery, RoundCompletionQuery>();
        services.AddScoped<IEarlierRoundStatusesQuery, EarlierRoundStatusesQuery>();
        services.AddScoped<IOverallLeaderboardQuery, OverallLeaderboardQuery>();
        services.AddScoped<IMonthlyLeaderboardQuery, MonthlyLeaderboardQuery>();
        services.AddScoped<IExactScoresLeaderboardQuery, ExactScoresLeaderboardQuery>();
        services.AddScoped<IStageLeaderboardQuery, StageLeaderboardQuery>();
        services.AddScoped<ILeagueRoundResultsQuery, LeagueRoundResultsQuery>();
        services.AddScoped<IDashboardLeaderboardsQuery, DashboardLeaderboardsQuery>();
        services.AddScoped<IMyLeaguesQuery, MyLeaguesQuery>();
        services.AddScoped<IJoinableLeaguesQuery, JoinableLeaguesQuery>();
        services.AddScoped<IMyLeagueRequestsQuery, MyLeagueRequestsQuery>();
        services.AddScoped<IAdminPendingMembersQuery, AdminPendingMembersQuery>();
        services.AddScoped<IActiveRoundsQuery, ActiveRoundsQuery>();
        services.AddScoped<ILeagueMembershipQuery, LeagueMembershipQuery>();
        services.AddScoped<ILeagueDashboardQuery, LeagueDashboardQuery>();
        services.AddScoped<ILeagueSeasonRoundsQuery, LeagueSeasonRoundsQuery>();
        services.AddScoped<ILeagueRoundsQuery, LeagueRoundsQuery>();
        services.AddScoped<ILeagueDetailQuery, LeagueDetailQuery>();
        services.AddScoped<ILeagueMembersQuery, LeagueMembersQuery>();
        services.AddScoped<ILeagueJoinCandidatesQuery, LeagueJoinCandidatesQuery>();
        services.AddScoped<ILeaguePaymentInfoQuery, LeaguePaymentInfoQuery>();
        services.AddScoped<ILeaguePrizesPageQuery, LeaguePrizesPageQuery>();
        services.AddScoped<ILeaguePayoutsQuery, LeaguePayoutsQuery>();
        services.AddScoped<IWinningsQuery, WinningsQuery>();
        services.AddScoped<ISeasonLookupQuery, SeasonLookupQuery>();
        services.AddScoped<IEmailSettingsQuery, EmailSettingsQuery>();
        services.AddScoped<ILeagueRecordsQuery, LeagueRecordsQuery>();
        services.AddScoped<ISeasonRecapQuery, SeasonRecapQuery>();
        services.AddScoped<IBadgeStateQuery, BadgeStateQuery>();
        services.AddScoped<IBadgeLeaderboardQuery, BadgeLeaderboardQuery>();
        services.AddScoped<IRoundMatchesQuery, RoundMatchesQuery>();
        services.AddScoped<IAdminSeasonRoundsQuery, AdminSeasonRoundsQuery>();
        services.AddScoped<IAdminRoundQuery, AdminRoundQuery>();
        services.AddScoped<IRoundDigestQuery, RoundDigestQuery>();
        services.AddScoped<ICompetitionsQuery, CompetitionsQuery>();
        services.AddScoped<ITeamsQuery, TeamsQuery>();
        services.AddScoped<ISeasonTeamsQuery, SeasonTeamsQuery>();
        services.AddScoped<IPricingSettingsQuery, PricingSettingsQuery>();
        services.AddScoped<IRunningCostsQuery, RunningCostsQuery>();
        services.AddScoped<IServiceFeesQuery, ServiceFeesQuery>();
        services.AddScoped<IEmailTestUserQuery, EmailTestUserQuery>();
        services.AddScoped<ISeasonsQuery, SeasonsQuery>();
        services.AddScoped<IAdminUsersQuery, AdminUsersQuery>();
        services.AddScoped<IUserDeletionImpactQuery, UserDeletionImpactQuery>();
        services.AddScoped<ISeasonPassPagesQuery, SeasonPassPagesQuery>();
        services.AddScoped<ISeasonPassHoldersQuery, SeasonPassHoldersQuery>();
        services.AddScoped<ISeasonPricingQuery, SeasonPricingQuery>();
        services.AddScoped<IRoundHeaderQuery, RoundHeaderQuery>();
        services.AddScoped<IUserRoundPredictionsQuery, UserRoundPredictionsQuery>();
        services.AddScoped<IPredictionLeaguesQuery, PredictionLeaguesQuery>();
        services.AddScoped<IShareCardPlayerQuery, ShareCardPlayerQuery>();
        services.AddScoped<ILeagueEmailRecipientQuery, LeagueEmailRecipientQuery>();
        services.AddScoped<IManageLeaguesQuery, ManageLeaguesQuery>();
        services.AddScoped<ILeagueBankDetailsQuery, LeagueBankDetailsQuery>();
        services.AddScoped<IAccountProfileQuery, AccountProfileQuery>();
        services.AddScoped<IMyPayoutDetailsQuery, MyPayoutDetailsQuery>();
        services.AddScoped<IOnboardingStateQuery, OnboardingStateQuery>();
        services.AddScoped<IHomepageSeasonsQuery, HomepageSeasonsQuery>();
        services.AddScoped<IPrizeSchemeSeasonQuery, PrizeSchemeSeasonQuery>();
        services.AddScoped<IPrizeWinnersQuery, PrizeWinnersQuery>();
        services.AddScoped<IPrizeEvaluationInputsQuery, PrizeEvaluationInputsQuery>();
        services.AddScoped<ILeagueWelcomeBatchQuery, LeagueWelcomeBatchQuery>();
    }

    // Every IXxxRepository in Application, in the order Application declares them. A new repository
    // interface with no registration here is caught by ThePredictions.Composition.Tests.Unit, which
    // resolves every handler from the real container.
    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<ICompetitionRepository, CompetitionRepository>();
        services.AddScoped<IRunningCostRepository, RunningCostRepository>();
        services.AddScoped<IPricingSettingsRepository, PricingSettingsRepository>();
        services.AddScoped<IServiceFeeRepository, ServiceFeeRepository>();
        services.AddScoped<ILeagueRepository, LeagueRepository>();
        services.AddScoped<ILeagueMemberRepository, LeagueMemberRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IEmailConfirmationTokenRepository, EmailConfirmationTokenRepository>();
        services.AddScoped<IRoundRepository, RoundRepository>();
        services.AddScoped<ISeasonRepository, SeasonRepository>();
        services.AddScoped<ISeasonPassRepository, SeasonPassRepository>();
        services.AddScoped<IOnboardingSkipRepository, OnboardingSkipRepository>();
        services.AddScoped<IUserBadgeRepository, UserBadgeRepository>();
        services.AddScoped<IBadgeEvaluationRepository, BadgeEvaluationRepository>();
        services.AddScoped<IUserPayoutDetailsRepository, UserPayoutDetailsRepository>();
        services.AddScoped<ILeaguePayoutRepository, LeaguePayoutRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ITournamentRoundMappingRepository, TournamentRoundMappingRepository>();
        services.AddScoped<IUserPredictionRepository, UserPredictionRepository>();
        services.AddScoped<IWinningsRepository, WinningsRepository>();
        services.AddScoped<IPrizeNotificationRepository, PrizeNotificationRepository>();
        services.AddScoped<ILeagueWelcomeNotificationRepository, LeagueWelcomeNotificationRepository>();
        services.AddScoped<IPredictionReminderNotificationRepository, PredictionReminderNotificationRepository>();
        services.AddScoped<IEmailSettingsRepository, EmailSettingsRepository>();
        services.AddScoped<IBoostReadRepository, BoostReadRepository>();
        services.AddScoped<IBoostWriteRepository, BoostWriteRepository>();
        services.AddScoped<ILeagueBoostRuleRepository, LeagueBoostRuleRepository>();
        services.AddScoped<ILeagueStatsRepository, LeagueStatsRepository>();
    }
}
