using PredictionLeague.API;
using PredictionLeague.API.Middleware;
using PredictionLeague.Infrastructure;
using PredictionLeague.Infrastructure.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var datadogApiKey = builder.Configuration["Datadog:ApiKey"];

builder.Services.AddControllers();
builder.Services.AddInfrastructureServices();
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddHostedService<DatabaseInitialiser>();
builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration)
        .WriteTo.DatadogLogs(
            apiKey: datadogApiKey,
            service: "prediction-league-web",
            source: "csharp",
            host: "production");
});

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();