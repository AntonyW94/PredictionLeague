using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using PredictionLeague.API;
using PredictionLeague.API.Middleware;
using PredictionLeague.Infrastructure;
using PredictionLeague.Infrastructure.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")));

builder.Services.AddControllers();
builder.Services.AddInfrastructureServices();
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddHostedService<DatabaseInitialiser>();
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
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
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseCookiePolicy();
app.UseRouting();
app.UseCors("AllowSpecificOrigin");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();