using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using PredictionLeague.Infrastructure.Data;
using PredictionLeague.Infrastructure.Identity;
using PredictionLeague.Infrastructure.Repositories;
using PredictionLeague.Infrastructure.Services;

namespace PredictionLeague.Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IDbConnectionFactory, SqlConnectionFactory>();

        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddUserStore<DapperUserStore>()
            .AddRoleStore<DapperRoleStore>()
            .AddSignInManager<SignInManager<ApplicationUser>>()
            .AddDefaultTokenProviders();

        services.AddScoped<ILeagueRepository, LeagueRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRoundRepository, RoundRepository>();
        services.AddScoped<IRoundResultRepository, RoundResultRepository>();
        services.AddScoped<ISeasonRepository, SeasonRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IUserPredictionRepository, UserPredictionRepository>();

        services.AddScoped<IAuthenticationTokenService, AuthenticationTokenService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddScoped<IPredictionService, PredictionService>();
    }
}