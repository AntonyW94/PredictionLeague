using FluentAssertions;
using FluentAssertions.Execution;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThePredictions.API;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Application.Configuration;
using ThePredictions.Infrastructure;
using Xunit;

namespace ThePredictions.Composition.Tests.Unit;

/// <summary>
/// Guards the DI composition root. The host registers services with
/// <c>AddInfrastructureServices</c> + <c>AddApiServices</c>; a MediatR handler that depends on an
/// unregistered service only fails at app startup (the Development host's <c>ValidateOnBuild</c>),
/// never at <c>dotnet build</c> or in handler unit tests, which construct handlers with mocks.
/// This test builds the real container and resolves every handler so that gap surfaces in CI
/// instead of on deploy.
/// </summary>
public class ContainerValidationTests
{
    /// <summary>
    /// Representative configuration covering every value the registration methods read at
    /// registration time or in a singleton constructor that gets eagerly built.
    /// </summary>
    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DataConnection"] = "Server=(local);Database=ValidationOnly;Trusted_Connection=True;TrustServerCertificate=True",
                ["JwtSettings:Secret"] = "composition-test-signing-secret-of-ample-length-0123456789",
                ["JwtSettings:Issuer"] = "test-issuer",
                ["JwtSettings:Audience"] = "test-audience",
                ["JwtSettings:ExpiryMinutes"] = "15",
                ["JwtSettings:RefreshTokenExpiryDays"] = "30",
                ["Authentication:Google:ClientId"] = "test-client-id",
                ["Authentication:Google:ClientSecret"] = "test-client-secret",
                ["FieldEncryption:Key"] = Convert.ToBase64String(new byte[32]),
                ["Brevo:ApiKey"] = "test-brevo-key",
                ["Brevo:SendFromName"] = "The Predictions",
                ["Brevo:SendFromEmail"] = "test@thepredictions.co.uk",
                ["FootballApi:ApiKey"] = "test-football-key",
                ["FootballApi:BaseUrl"] = "https://stub.football.test/",
                ["Timeouts:FootballApiTimeoutSeconds"] = "30",
                ["ApiBaseUrl"] = "https://test.thepredictions.co.uk"
            })
            .Build();

    private static ServiceProvider BuildHostProvider()
    {
        var services = new ServiceCollection();

        // Ambient services a WebApplicationBuilder provides for free; the registration methods and
        // ASP.NET Identity depend on them, so a bare ServiceCollection must add them itself.
        var configuration = BuildConfiguration();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddDataProtection();

        services.AddInfrastructureServices(configuration);
        services.AddApiServices(configuration);

        // Options the host binds in Program.cs (outside the two Add* methods). The football
        // settings matter here: its typed HttpClient sets BaseAddress from FootballApi:BaseUrl in
        // its constructor, so a handler depending on IFootballDataService can't be built without it.
        services.Configure<BrevoSettings>(configuration.GetSection("Brevo"));
        services.Configure<FootballApiSettings>(configuration.GetSection("FootballApi"));
        services.Configure<FootballApiResilienceSettings>(configuration.GetSection("FootballApi:Resilience"));
        services.Configure<TimeoutSettings>(configuration.GetSection("Timeouts"));
        services.Configure<SiteSettings>(options => options.BaseUrl = configuration["ApiBaseUrl"]);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void EveryMediatrHandler_ShouldResolveWithAllDependenciesRegistered()
    {
        using var provider = BuildHostProvider();
        using var scope = provider.CreateScope();

        var handlerInterfaces = typeof(IAssemblyMarker).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetInterfaces())
            .Where(i => i.IsGenericType &&
                        (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) ||
                         i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)))
            .Distinct()
            .ToList();

        handlerInterfaces.Should().NotBeEmpty("the Application assembly defines MediatR handlers");

        // Resolving each handler constructs its full dependency chain (repositories, services,
        // options); a missing registration throws InvalidOperationException here, exactly as it
        // would at host startup. AssertionScope reports every broken handler in one run.
        using var assertionScope = new AssertionScope();
        foreach (var handlerInterface in handlerInterfaces)
        {
            var act = () => scope.ServiceProvider.GetRequiredService(handlerInterface);
            act.Should().NotThrow($"{handlerInterface} must resolve with all constructor dependencies registered");
        }
    }
}
