using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using PredictionLeague.API.Services;
using PredictionLeague.Application.Common.Behaviours;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Application.Services;
using PredictionLeague.Infrastructure.Authentication.Settings;
using PredictionLeague.Validators.Authentication;
using System.Text;

namespace PredictionLeague.API;

public static class DependencyInjection
{
    public static void AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        var jwtSettings = new JwtSettings();
        configuration.Bind(JwtSettings.SectionName, jwtSettings);
        services.AddSingleton(jwtSettings);

        services.AddRateLimiting();
        services.AddAppAuthentication(configuration);
        services.AddApplicationServices(configuration);
    }

    private static void AddAppAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;
        var googleSettings = configuration.GetSection(GoogleAuthSettings.SectionName).Get<GoogleAuthSettings>()!;

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
                };
            })
            .AddGoogle(options =>
            {
                options.ClientId = googleSettings.ClientId;
                options.ClientSecret = googleSettings.ClientSecret;
                options.CallbackPath = "/signin-google";
                options.SignInScheme = IdentityConstants.ExternalScheme;
            });

        services.AddAuthorization();
    }

    private static void AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

        // Add HttpContextAccessor for CurrentUserService
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(IAssemblyMarker).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehaviour<,>));

            var mediatRKey = configuration["MediatR:LicenceKey"];
            if (!string.IsNullOrEmpty(mediatRKey))
                cfg.LicenseKey = mediatRKey;
        });
    }

    private static void AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Global rate limit - 100 requests per minute per IP
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientIpAddress(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Authentication endpoints - stricter limits to prevent brute-force
            options.AddPolicy("auth", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientIpAddress(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(5),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // API endpoints - moderate limits for normal usage
            options.AddPolicy("api", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientIpAddress(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 2
                    }));

            // Configure rejection response
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                await context.HttpContext.Response.WriteAsync(
                    "Too many requests. Please try again later.",
                    cancellationToken);
            };
        });
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // Check X-Forwarded-For header first (common for reverse proxies)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP in the chain (original client)
            var ip = forwardedFor.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(ip))
                return ip;
        }

        // Fall back to direct connection IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}