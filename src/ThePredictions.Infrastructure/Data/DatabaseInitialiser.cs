using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Infrastructure.Data;

[ExcludeFromCodeCoverage(Justification = "Database plumbing: connection, transaction and type-handler wiring with no branching logic of its own.")]
public class DatabaseInitialiser(IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
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
