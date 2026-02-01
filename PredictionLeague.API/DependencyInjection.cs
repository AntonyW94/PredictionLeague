using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                // ===========================================
                // API INFORMATION
                // ===========================================
                // This appears at the top of Swagger UI and in the OpenAPI spec
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "The Predictions API",
                    Description = """
                        ## Overview
                        API for The Predictions football prediction platform. Allows users to create leagues,
                        submit match predictions, track leaderboards, and manage prizes.

                        ## Authentication
                        Most endpoints require JWT Bearer authentication. Include the token in the Authorization header:
                        ```
                        Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
                        ```

                        To obtain a token, use the `/api/auth/login` or `/api/auth/register` endpoints.

                        ## Scheduled Tasks
                        Endpoints under `/api/tasks/` require API Key authentication via the `X-Api-Key` header.
                        These are intended for scheduled jobs (cron) and should not be called directly by users.

                        ## Rate Limiting
                        - **Global:** 100 requests per minute per IP
                        - **Auth endpoints:** 10 requests per 5 minutes per IP
                        - **API endpoints:** 60 requests per minute per IP
                        """,
                    Contact = new OpenApiContact
                    {
                        Name = "The Predictions Support",
                        Email = "support@thepredictions.co.uk",
                        Url = new Uri("https://www.thepredictions.co.uk")
                    },
                    License = new OpenApiLicense
                    {
                        Name = "Proprietary",
                        Url = new Uri("https://www.thepredictions.co.uk/terms")
                    }
                });

                // ===========================================
                // ENABLE ANNOTATIONS
                // ===========================================
                // This enables [SwaggerOperation], [SwaggerResponse], [SwaggerParameter] attributes
                options.EnableAnnotations();

                // ===========================================
                // JWT BEARER AUTHENTICATION
                // ===========================================
                // This adds the "Authorize" button to Swagger UI for JWT tokens
                //
                // How it works:
                // 1. User clicks "Authorize" button in Swagger UI
                // 2. User enters their JWT token (without "Bearer " prefix)
                // 3. Swagger automatically adds "Authorization: Bearer {token}" header to all requests
                // 4. Protected endpoints can now be tested directly in Swagger UI
                //
                // The SecuritySchemeType.Http with "bearer" scheme handles the "Bearer " prefix automatically
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Description = """
                        JWT Bearer token authentication.

                        **How to authenticate:**
                        1. Call `/api/auth/login` with your credentials
                        2. Copy the `accessToken` from the response
                        3. Click 'Authorize' and paste the token (without 'Bearer ' prefix)
                        4. Click 'Authorize' to apply

                        Token expires after 15 minutes. Use `/api/auth/refresh-token` to obtain a new token.
                        """,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header
                });

                // Note: No AddSecurityRequirement needed - Swashbuckle automatically detects
                // [Authorize] attributes and shows padlock icons on protected endpoints

                // ===========================================
                // API KEY AUTHENTICATION (for scheduled tasks)
                // ===========================================
                // This adds a second authentication option for API key-protected endpoints
                //
                // How it works:
                // 1. Scheduled task endpoints (TasksController) use [ApiKeyAuthorise] attribute
                // 2. These endpoints expect "X-Api-Key" header with a valid API key
                // 3. In Swagger UI, user clicks "Authorize" and enters the API key
                // 4. Swagger adds "X-Api-Key: {key}" header to requests
                //
                // Note: API key endpoints don't require JWT - they use a separate auth mechanism
                options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
                {
                    Name = "X-Api-Key",
                    Description = """
                        API Key authentication for scheduled task endpoints.

                        **Usage:**
                        This is used by cron jobs to trigger scheduled tasks like:
                        - Score updates from football API
                        - Email reminders
                        - Data synchronisation

                        Regular users should not need to use these endpoints.
                        """,
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Scheme = "ApiKeyScheme"
                });

                // ===========================================
                // OPERATION FILTERS (Optional enhancements)
                // ===========================================
                // Tag endpoints by controller for better organization in Swagger UI
                options.TagActionsBy(api =>
                {
                    if (api.GroupName != null)
                        return [api.GroupName];

                    var controllerName = api.ActionDescriptor.RouteValues["controller"];

                    // Group admin controllers under "Admin" prefix
                    if (api.RelativePath?.StartsWith("api/admin/") == true)
                        return [$"Admin - {controllerName}"];

                    return [controllerName ?? "Default"];
                });

                // Sort tags alphabetically
                options.OrderActionsBy(api => api.RelativePath);
            });

            var jwtSettings = new JwtSettings();
            configuration.Bind(JwtSettings.SectionName, jwtSettings);
            services.AddSingleton(jwtSettings);

            services.AddRateLimiting();
            services.AddAppAuthentication(configuration);
            services.AddApplicationServices(configuration);

            return services;
        }

        private static IServiceCollection AddAppAuthentication(this IServiceCollection services, IConfiguration configuration)
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

            return services;
        }

        private static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
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

            return services;
        }

        private static IServiceCollection AddRateLimiting(this IServiceCollection services)
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

            return services;
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