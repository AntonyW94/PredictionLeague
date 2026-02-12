using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Features.Admin.Rounds.Strategies;
using PredictionLeague.Application.Formatters;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Application.Services.Boosts;
using PredictionLeague.Domain.Models;
using PredictionLeague.Infrastructure.Data;
using PredictionLeague.Infrastructure.Formatters;
using PredictionLeague.Infrastructure.Identity;
using PredictionLeague.Infrastructure.Repositories;
using PredictionLeague.Infrastructure.Repositories.Boosts;
using PredictionLeague.Infrastructure.Services;
using System.Net;

namespace PredictionLeague.Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IApplicationReadDbConnection, DapperReadDbConnection>();

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Password policy
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 4;

                // Lockout settings
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                // User settings
                options.User.RequireUniqueEmail = true;
            })
            .AddUserStore<DapperUserStore>()
            .AddRoleStore<DapperRoleStore>()
            .AddSignInManager<SignInManager<ApplicationUser>>()
            .AddDefaultTokenProviders();
      
        services.ConfigureApplicationCookie(options =>
        {
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                else
                    context.Response.Redirect(context.RedirectUri);
                
                return Task.CompletedTask;
            };
        });
        
        services.AddScoped<ILeagueRepository, LeagueRepository>();
        services.AddScoped<ILeagueMemberRepository, LeagueMemberRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IRoundRepository, RoundRepository>();
        services.AddScoped<ISeasonRepository, SeasonRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IUserPredictionRepository, UserPredictionRepository>();
        services.AddScoped<IWinningsRepository, WinningsRepository>();
        services.AddScoped<IBoostReadRepository, BoostReadRepository>();
        services.AddScoped<IBoostWriteRepository, BoostWriteRepository>(); 
        services.AddScoped<ILeagueStatsRepository, LeagueStatsRepository>();
        services.AddScoped<IPrizeStrategy, RoundPrizeStrategy>();
        services.AddScoped<IPrizeStrategy, MonthlyPrizeStrategy>();
        services.AddScoped<IPrizeStrategy, OverallPrizeStrategy>();
        services.AddScoped<IPrizeStrategy, MostExactScoresPrizeStrategy>();

        services.AddScoped<PredictionDomainService>();
        services.AddSingleton<IEmailDateFormatter, UkEmailDateFormatter>();
        
        services.AddScoped<IAuthenticationTokenService, AuthenticationTokenService>(); 
        services.AddScoped<IEmailService, BrevoEmailService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<IBoostService, BoostService>();
        services.AddScoped<IUserManager, UserManagerService>();
        services.AddHttpClient<IFootballDataService, FootballDataService>();
        services.AddScoped<ILeagueStatsService, LeagueStatsService>();
        services.AddScoped<ILeagueMembershipService, LeagueMembershipService>();
    }
}