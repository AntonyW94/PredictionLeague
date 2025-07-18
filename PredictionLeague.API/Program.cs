using PredictionLeague.API;
using PredictionLeague.API.Middleware;
using PredictionLeague.Infrastructure;
using PredictionLeague.Infrastructure.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureServices();
builder.Services.AddPresentationServices(builder.Configuration);
builder.Services.AddAuthenticationServices(builder.Configuration);
builder.Services.AddHostedService<DatabaseInitialiser>();
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowSpecificOrigin");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();