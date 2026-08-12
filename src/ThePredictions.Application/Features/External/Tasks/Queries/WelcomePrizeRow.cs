using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.External.Tasks.Queries;

/// <summary>One prize a league pays, as the welcome email lists it.</summary>
/// <remarks>
/// These rows do two jobs: they are the list the email shows, and they are how a half-configured league is recognised - one with a
/// scheme recorded but no prize settings worked out from it yet.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record WelcomePrizeRow(int LeagueId, PrizeType PrizeType, int Rank, string? Stage, decimal Amount);
