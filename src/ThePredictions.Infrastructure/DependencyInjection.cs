using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.Rounds.Strategies;
using ThePredictions.Application.Formatters;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Application.Services.Boosts;
using ThePredictions.Application.Services.Payments;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;
using ThePredictions.Domain.Services;
using ThePredictions.Infrastructure.Formatters;
using ThePredictions.Infrastructure.HealthChecks;
using ThePredictions.Infrastructure.Identity;
using ThePredictions.Infrastructure.Resilience;
using ThePredictions.Infrastructure.Services;
using ThePredictions.Infrastructure.Services.Payments;
using System.Net;

namespace ThePredictions.Infrastructure;

[ExcludeFromCodeCoverage(Justification = "Container registration: verified by ThePredictions.Composition.Tests.Unit, which resolves every handler from the real container.")]
public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FieldEncryptionSettings>(
            configuration.GetSection(FieldEncryptionSettings.SectionName));
        services.AddSingleton<IFieldEncryptionService, FieldEncryptionService>();

        services.Configure<StripeSettings>(
            configuration.GetSection(StripeSettings.SectionName));
        services.AddScoped<IPaymentService, StripePaymentService>();

        // The connection, transaction and read seams, plus the database health probe, are registered by
        // ThePredictions.Persistence.SqlServer.AddSqlServerPersistence. Both calls are made by the
        // composition root; AddHealthChecks() composes, so the two checks share one registry.
        services.AddHealthChecks()
            .AddCheck<FootballApiHealthCheck>("football-api", tags: ["ready"]);

        services.AddHttpClient<FootballApiHealthCheck>();

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
            // No AddUserStore/AddRoleStore here: the stores are the persistence adapter's, and
            // AddSqlServerPersistence registers IUserStore<ApplicationUser> and IRoleStore<IdentityRole>
            // directly, which is exactly what those two builder methods do. What stays here is the part
            // that is not persistence - password policy, lockout, the sign-in manager and the token
            // providers - so a different adapter changes the stores and nothing else.
            .AddSignInManager<SignInManager<ApplicationUser>>()
            .AddDefaultTokenProviders();

        // Canonicalise emails (strip +alias) so plus-aliases collide on the unique email index (ADR 0009).
        services.AddScoped<ILookupNormalizer, CanonicalEmailLookupNormalizer>();

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

        services.AddScoped<IPrizeStrategy, RoundPrizeStrategy>();
        services.AddScoped<IPrizeStrategy, MonthlyPrizeStrategy>();
        services.AddScoped<IPrizeStrategy, OverallPrizeStrategy>();
        services.AddScoped<IPrizeStrategy, MostExactScoresPrizeStrategy>();
        services.AddScoped<IPrizeStrategy, SectionPrizeStrategy>();

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<PredictionDomainService>();
        services.AddSingleton<IEmailDateFormatter, UkEmailDateFormatter>();

        services.AddMemoryCache();

        services.AddScoped<IAuthenticationTokenService, AuthenticationTokenService>();
        services.AddScoped<IEmailService, BrevoEmailService>();
        services.AddScoped<IEmailSettingsProvider, CachedEmailSettingsProvider>();
        services.AddScoped<IEmailTemplateCatalog, BrevoEmailTemplateCatalog>();
        services.AddSingleton<IEmailTestDefaultsResolver, EmailTestDefaultsResolver>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<IBoostService, BoostService>();
        services.AddScoped<IUserManager, UserManagerService>();
        services.AddHttpClient<IFootballDataService, FootballDataService>((serviceProvider, client) =>
        {
            var timeoutSettings = serviceProvider.GetRequiredService<IOptions<TimeoutSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(timeoutSettings.FootballApiTimeoutSeconds);
        })
            .AddResilienceHandler("FootballApi", FootballApiResilienceConfiguration.Configure);

        services.AddHttpClient<IShareCardRenderer, ShareCardRenderer>(client =>
        {
            // Team logos are small remote assets; a short timeout keeps a slow logo host from
            // stalling the share-card response - a missing logo just falls back to a badge.
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddSingleton<IBadgeIconRenderer, BadgeIconRenderer>();
        services.AddScoped<IBadgeAwardService, BadgeAwardService>();

        services.AddScoped<ILeagueMembershipService, Application.Services.LeagueMembershipService>();
        services.AddScoped<ISeasonAccessService, SeasonAccessService>();
        services.AddScoped<ISeasonPriceRecommendationService, SeasonPriceRecommendationService>();
        services.AddScoped<IEmailConfirmationSender, EmailConfirmationSender>();
    }
}
