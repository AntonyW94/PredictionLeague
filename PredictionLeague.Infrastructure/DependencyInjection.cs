﻿using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using PredictionLeague.Domain.Services;
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
        services.AddScoped<IApplicationReadDbConnection, DapperReadDbConnection>();

        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddUserStore<DapperUserStore>()
            .AddRoleStore<DapperRoleStore>()
            .AddSignInManager<SignInManager<ApplicationUser>>()
            .AddDefaultTokenProviders();

        services.AddScoped<ILeagueRepository, LeagueRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRoundRepository, RoundRepository>();
        services.AddScoped<ISeasonRepository, SeasonRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IUserPredictionRepository, UserPredictionRepository>();

        services.AddScoped<PredictionDomainService>();
        
        services.AddScoped<IAuthenticationTokenService, AuthenticationTokenService>(); 
    }
}