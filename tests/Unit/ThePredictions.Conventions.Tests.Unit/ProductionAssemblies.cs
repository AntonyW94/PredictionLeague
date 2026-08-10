using System.Reflection;

namespace ThePredictions.Conventions.Tests.Unit;

/// <summary>
/// The set of production assemblies every convention in this project sweeps, plus the repository root
/// for the two rules that have to read source. Loading is forced through a type reference per assembly
/// rather than <c>AppDomain.CurrentDomain.GetAssemblies()</c>, because an assembly the test host has
/// not touched yet would otherwise be silently absent and the sweep would quietly pass.
/// </summary>
internal static class ProductionAssemblies
{
    internal static readonly Assembly Domain = typeof(Domain.Models.League).Assembly;
    internal static readonly Assembly Contracts = typeof(Contracts.Leagues.LeagueDto).Assembly;
    internal static readonly Assembly Application = typeof(Application.Common.Interfaces.IAssemblyMarker).Assembly;
    // Anchored on each assembly's DependencyInjection: every adapter has one, and unlike a repository or
    // a connection factory it cannot move to another project without the assembly itself moving. The
    // persistence split broke the previous anchor (Infrastructure.Repositories.LeagueRepository) by
    // relocating exactly the type it named.
    internal static readonly Assembly Infrastructure = typeof(Infrastructure.DependencyInjection).Assembly;
    internal static readonly Assembly PersistenceSqlServer = typeof(Persistence.SqlServer.DependencyInjection).Assembly;
    internal static readonly Assembly Api = typeof(API.Controllers.ApiControllerBase).Assembly;
    internal static readonly Assembly Validators = typeof(Validators.Leagues.CreateLeagueRequestValidator).Assembly;
    internal static readonly Assembly WebClient = typeof(Web.Client.ViewModels.Admin.Rounds.MatchViewModel).Assembly;
    internal static readonly Assembly HostingShared = typeof(Hosting.Shared.Extensions.ConfigurationSubstitutionExtensions).Assembly;

    internal static readonly IReadOnlyList<Assembly> All =
    [
        Domain, Contracts, Application, Infrastructure, PersistenceSqlServer, Api, Validators, WebClient,
        HostingShared
    ];

    /// <summary>
    /// Server-side assemblies only: those resolved from the DI container, where an injected
    /// <c>IDateTimeProvider</c> is reachable. Web.Client is excluded deliberately - it runs in
    /// WebAssembly with no such registration, and its countdown components legitimately read the
    /// wall clock.
    /// </summary>
    internal static readonly IReadOnlyList<Assembly> ServerSide =
    [
        Domain, Contracts, Application, Infrastructure, PersistenceSqlServer, Api, Validators, HostingShared
    ];

    /// <summary>Absolute path to the repository root, injected by the csproj as assembly metadata.</summary>
    internal static string RepositoryRoot { get; } = ResolveRepositoryRoot();

    private static string ResolveRepositoryRoot()
    {
        var root = typeof(ProductionAssemblies).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(a => a.Key == "RepositoryRoot")
            ?.Value;

        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("The RepositoryRoot assembly metadata is missing; see this project's csproj.");

        return Path.GetFullPath(root);
    }

    /// <summary>
    /// Every hand-written C# and Razor file under src/, excluding build output. Paths are returned
    /// repository-relative with forward slashes so allowlists read the same on every machine.
    /// </summary>
    internal static IEnumerable<(string RelativePath, string Text)> SourceFiles(params string[] extensions)
    {
        var sourceRoot = Path.Combine(RepositoryRoot, "src");

        if (!Directory.Exists(sourceRoot))
            throw new InvalidOperationException($"Expected to find the source tree at '{sourceRoot}'.");

        foreach (var extension in extensions)
        {
            foreach (var path in Directory.EnumerateFiles(sourceRoot, $"*{extension}", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/');

                if (relative.Contains("/obj/") || relative.Contains("/bin/"))
                    continue;

                yield return (relative, File.ReadAllText(path));
            }
        }
    }
}
