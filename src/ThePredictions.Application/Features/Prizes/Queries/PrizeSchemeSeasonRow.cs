using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Prizes.Queries;

/// <summary>The shape of a season, as prize evaluation needs it.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PrizeSchemeSeasonRow(int NumberOfRounds, DateTime StartDateUtc, DateTime EndDateUtc);
