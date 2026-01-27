using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using PredictionLeague.API;
using PredictionLeague.API.Middleware;
using PredictionLeague.Application.Configuration;
using PredictionLeague.Hosting.Shared.Extensions;
using PredictionLeague.Infrastructure;
using PredictionLeague.Infrastructure.Data;
using Serilog;

const string corsName = "ThePredictionsCors";

var builder = WebApplication.CreateBuilder(args);

var keyVaultUri = builder.Configuration["KeyVaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    if (builder.Environment.IsProduction())
    {
        builder.Configuration.AddJsonFile("appsettings.Production.Secrets.json", optional: false, reloadOnChange: true);

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
                .WithHeaders("Content-Type", "Authorization", "Accept", "X-Api-Key", "X-Requested-With")
                .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                .AllowCredentials();
        });
});

builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")));

builder.Services.AddControllers();
builder.Services.AddInfrastructureServices();
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddHostedService<DatabaseInitialiser>();

builder.Services.Configure<BrevoSettings>(builder.Configuration.GetSection("Brevo"));
builder.Services.Configure<FootballApiSettings>(builder.Configuration.GetSection("FootballApi"));

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

Dapper.SqlMapper.AddTypeHandler(new DapperUtcDateTimeHandler());

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownIPNetworks = { new System.Net.IPNetwork(System.Net.IPAddress.Parse("10.44.44.0"), 24) }
});

if (app.Environment.IsDevelopment())
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
        if (path != null && (path.StartsWith("/_framework") || path.StartsWith("/_blazor")))
            return Serilog.Events.LogEventLevel.Verbose;

        return Serilog.Events.LogEventLevel.Information;
    };
});

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors(corsName);
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseCookiePolicy();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();