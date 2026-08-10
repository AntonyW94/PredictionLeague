using ThePredictions.API;
using ThePredictions.API.Middleware;
using ThePredictions.Application.Configuration;
using ThePredictions.Infrastructure;
using ThePredictions.Infrastructure.HealthChecks;
using ThePredictions.Persistence.SqlServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSqlServerPersistence(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

// Site base URL for absolute links in emails. Bound the same way as the Web host so the link
// builders never fall back silently; when ApiBaseUrl is unset the resolver uses the safe canonical
// site (SiteSettings.FallbackBaseUrl), never a request header. (The Web host binds this per environment;
// centralising options binding across both hosts is tracked in the composition-root-and-hosting plan.)
builder.Services.Configure<SiteSettings>(options => options.BaseUrl = builder.Configuration["ApiBaseUrl"]);

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseSecurityHeaders();

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthCheckEndpoints();

app.Run();