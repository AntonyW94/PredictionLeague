using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using ThePredictions.API;
using ThePredictions.API.Middleware;
using ThePredictions.Application.Configuration;
using ThePredictions.Hosting.Shared.Extensions;
using ThePredictions.Infrastructure;
using ThePredictions.Infrastructure.Data;
using ThePredictions.Infrastructure.HealthChecks;
using Serilog;

const string corsName = "ThePredictionsCors";

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsEnvironment("Local"))
{
    builder.WebHost.UseStaticWebAssets();
}

var keyVaultUri = builder.Configuration["KeyVaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    if (builder.Environment.IsProduction() || builder.Environment.IsDevelopment())
    {
        var secretsFile = $"appsettings.{builder.Environment.EnvironmentName}.Secrets.json";
        builder.Configuration.AddJsonFile(secretsFile, optional: false, reloadOnChange: true);

        var tenantId = builder.Configuration["AzureCredentials:TenantId"];
        var clientId = builder.Configuration["AzureCredentials:ClientId"];
        var clientSecret = builder.Configuration["AzureCredentials:ClientSecret"];

        var credentials = new ClientSecretCredential(tenantId, clientId, clientSecret);
        builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), credentials);
    }
    else
    {
        builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
    }
}

builder.Configuration.EnableSubstitutions();

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: corsName,
        policy =>
        {
            policy.WithOrigins(builder.Configuration["ApiBaseUrl"] ?? string.Empty)
                .WithHeaders("Content-Type", "Authorization", "Accept", "X-Api-Key", "X-Requested-With", "X-Correlation-Id")
                .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                .WithExposedHeaders("X-Correlation-Id")
                .AllowCredentials();
        });
});

builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")));

builder.Services.AddControllers();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddHostedService<DatabaseInitialiser>();

builder.Services.Configure<BrevoSettings>(builder.Configuration.GetSection("Brevo"));
builder.Services.Configure<EmailDeliverySettings>(builder.Configuration.GetSection("EmailDelivery"));
builder.Services.Configure<FootballApiSettings>(builder.Configuration.GetSection("FootballApi"));
builder.Services.Configure<FootballApiResilienceSettings>(builder.Configuration.GetSection("FootballApi:Resilience"));
builder.Services.Configure<TimeoutSettings>(builder.Configuration.GetSection("Timeouts"));
builder.Services.Configure<SiteSettings>(options => options.BaseUrl = builder.Configuration["ApiBaseUrl"]);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

Dapper.SqlMapper.AddTypeHandler(new DapperUtcDateTimeHandler());

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownNetworks = { new IPNetwork(System.Net.IPAddress.Parse("10.44.44.0"), 24) }
});

// Canonicalise the apex domain to the www subdomain (thepredictions.co.uk -> www.thepredictions.co.uk),
// preserving scheme, path and query. Sits after UseForwardedHeaders so the original host/scheme are seen.
// Only the exact apex is matched, so dev.* and localhost are untouched.
app.Use(async (context, next) =>
{
    var request = context.Request;
    if (string.Equals(request.Host.Host, "thepredictions.co.uk", StringComparison.OrdinalIgnoreCase))
    {
        var wwwHost = request.Host.Port.HasValue
            ? new HostString("www.thepredictions.co.uk", request.Host.Port.Value)
            : new HostString("www.thepredictions.co.uk");

        var location = $"{request.Scheme}://{wwwHost}{request.PathBase}{request.Path}{request.QueryString}";
        context.Response.Redirect(location, permanent: true);
        return;
    }

    await next();
});

var isLocalDev = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Local");
if (isLocalDev)
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, elapsed, ex) =>
    {
        var path = httpContext.Request.Path.Value;
        if (path != null && (path.StartsWith("/_framework") || path.StartsWith("/_blazor") || path.StartsWith("/health")))
            return Serilog.Events.LogEventLevel.Verbose;

        return Serilog.Events.LogEventLevel.Information;
    };
});

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors(corsName);
app.UseBlazorFrameworkFiles();

// Prevent browser caching of index.html and blazor.boot.json so deployments are always picked up.
// Mobile browsers aggressively cache framework files which can cause integrity check failures
// (yellow error banner) when blazor.boot.json expects new hashes but stale DLLs are served from cache.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var contentType = context.Response.ContentType;
        var path = context.Request.Path.Value;

        var isHtml = contentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true;
        var isBlazorBoot = path?.EndsWith("blazor.boot.json", StringComparison.OrdinalIgnoreCase) == true;

        if (isHtml || isBlazorBoot)
        {
            context.Response.Headers.CacheControl = "no-cache, no-store";
            context.Response.Headers.Pragma = "no-cache";
        }

        return Task.CompletedTask;
    });
    await next();
});

app.UseStaticFiles();
app.UseCookiePolicy();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthCheckEndpoints();
app.MapFallbackToFile("index.html");

app.Run();