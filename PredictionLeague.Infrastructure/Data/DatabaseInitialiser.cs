using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Infrastructure.Data;

public class DatabaseInitialiser : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public DatabaseInitialiser(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (ApplicationUserRole role in Enum.GetValues(typeof(ApplicationUserRole)))
        {
            var roleName = role.ToString();
             
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole(roleName));
                
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}