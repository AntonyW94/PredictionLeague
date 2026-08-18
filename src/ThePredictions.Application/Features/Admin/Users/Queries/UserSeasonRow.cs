using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>One season, and enough about its rounds to say whether it has finished.</summary>
/// <remarks>
/// Both counts rather than a flag, because whether a season has finished is a rule the whole application already shares -
/// every round it holds is complete, and it holds at least one - and settling it inside this read would create a second
/// definition of it. See <c>SeasonCompletion</c>.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record UserSeasonRow(
    int SeasonId,
    string Name,
    int RoundCount,
    int CompletedRoundCount);
