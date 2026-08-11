using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

/// <summary>One pass this player holds.</summary>
/// <remarks>
/// The tier arrives as stored. Whether it carries text-message reminders follows from it being the premium tier, which was
/// a <c>CASE WHEN sp.[Tier] = @PremiumTier</c> in the read.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record HeldSeasonPassRow(
    int SeasonId,
    string Tier,
    string Source,
    decimal AmountPaid,
    DateTime CreatedAtUtc);
