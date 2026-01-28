using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
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
    extension(IServiceCollection services)
    {
        public void AddApiServices(IConfiguration configuration)
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

        private void AddAppAuthentication(IConfiguration configuration)
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

        private void AddApplicationServices(IConfiguration configuration)
        {
            services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
            services.AddFluentValidationAutoValidation();
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

        private void AddRateLimiting()
        {
            services.AddRateLimiter(options =>
            {
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

                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                    }

                    await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", cancellationToken);
                };
            });
        }
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (string.IsNullOrEmpty(forwardedFor))
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
 
        var ip = forwardedFor.Split(',')[0].Trim();
        if (!string.IsNullOrEmpty(ip))
            return ip;

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}