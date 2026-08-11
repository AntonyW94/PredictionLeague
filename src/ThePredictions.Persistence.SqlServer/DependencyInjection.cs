using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Boosts.Queries;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;
using ThePredictions.Persistence.SqlServer.Identity;
using ThePredictions.Persistence.SqlServer.Data;
using ThePredictions.Persistence.SqlServer.Data.Resilience;
using ThePredictions.Persistence.SqlServer.Queries.Boosts;
using ThePredictions.Persistence.SqlServer.Queries.Leagues;
using ThePredictions.Persistence.SqlServer.Queries.Rounds;
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
