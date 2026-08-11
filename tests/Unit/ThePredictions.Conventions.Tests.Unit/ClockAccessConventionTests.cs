using FluentAssertions;
using Xunit;

namespace ThePredictions.Conventions.Tests.Unit;

/// <summary>
/// <c>DateTime.Now</c> is banned outright at build time (BannedSymbols.txt). This covers the softer
/// half: a direct <c>DateTime.UtcNow</c> read is correct but untestable, because a time-dependent
/// branch cannot be exercised without the clock cooperating. The audit that added this project found
/// two query handlers where exactly that had hidden a deadline rule from its tests - one of them
/// reading <c>UtcNow</c> twice, so the two reads could straddle a tick and disagree.
///
/// This is a burn-down allowlist, not a ban. Every entry is a call site that predates the rule; the
/// list may shrink but must never grow, so a new handler cannot quietly reintroduce the problem.
/// </summary>
public class ClockAccessConventionTests
{
    /// <summary>
    /// Known direct reads, with why each is still here. Removing an entry is the goal; adding one needs
    /// an argument in review.
    /// </summary>
    private static readonly string[] AllowedDirectUtcNowReads =
    [
        // Reads configuration to date a cookie, before any handler or container scope exists.
        "src/ThePredictions.API/Controllers/AuthControllerBase.cs",

        // FluentValidation builds its rule chain in the constructor, so the comparison value is captured
        // once at registration. Injecting a provider here needs the validator to take a dependency and
        // the rule to become a Must(...) - worth doing, not yet done.
        "src/ThePredictions.Validators/Leagues/CreateLeagueRequestValidator.cs",
        "src/ThePredictions.Validators/Leagues/UpdateLeagueRequestValidator.cs",

        // Straightforward to convert to IDateTimeProvider - these are the remaining burn-down targets.
        "src/ThePredictions.Application/Features/Admin/Rounds/Commands/UpdateScoresForNextRoundCommandHandler.cs",
        "src/ThePredictions.Application/Features/External/Tasks/Commands/CleanupExpiredDataCommandHandler.cs"
    ];

    /// <summary>
    /// The provider itself, whose whole job is to read the clock. Its <c>UtcNow</c> is the one legitimate
    /// call site in the solution.
    /// </summary>
    private const string ProviderFileNameFragment = "DateTimeProvider";

    private static IEnumerable<string> ServerSideFilesReadingUtcNowDirectly() =>
        ProductionAssemblies.SourceFiles(".cs")
            .Where(f => !f.RelativePath.Contains("/ThePredictions.Web.Client/", StringComparison.Ordinal))
            .Where(f => !f.RelativePath.Contains(ProviderFileNameFragment, StringComparison.Ordinal))
            .Where(f => f.Text.Contains("DateTime.UtcNow", StringComparison.Ordinal))
            .Select(f => f.RelativePath)
            .Distinct();

    [Fact]
    public void NoNewServerSideCode_ShouldReadDateTimeUtcNowDirectly()
    {
        var unexpected = ServerSideFilesReadingUtcNowDirectly()
            .Except(AllowedDirectUtcNowReads, StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        unexpected.Should().BeEmpty(
            "inject IDateTimeProvider instead of reading DateTime.UtcNow directly, so the time-dependent "
            + "branch can be tested. If the call site genuinely cannot reach the container, add it to "
            + "AllowedDirectUtcNowReads with the reason.");
    }

    // Keeps the allowlist honest: once a call site is converted, its entry must go, or the list silently
    // becomes a record of work already finished and stops being a burn-down.
    [Fact]
    public void TheAllowlist_ShouldNotContainCallSitesThatHaveAlreadyBeenConverted()
    {
        var stale = AllowedDirectUtcNowReads
            .Except(ServerSideFilesReadingUtcNowDirectly(), StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        stale.Should().BeEmpty(
            "these files no longer read DateTime.UtcNow directly - remove them from AllowedDirectUtcNowReads.");
    }
}
