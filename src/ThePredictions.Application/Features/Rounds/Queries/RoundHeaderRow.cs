using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Rounds.Queries;

/// <summary>One round, its season, and the competition behind it.</summary>
/// <remarks>
/// The competition type arrives as the enum. Both statements this replaces selected it as an <c>int</c> and compared it to
/// <c>(int)CompetitionType.Tournament</c> in C#, which is a cast that only works while nobody reorders the enum.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record RoundHeaderRow(
    int RoundId,
    int RoundNumber,
    string DisplayName,
    DateTime DeadlineUtc,
    int SeasonId,
    string SeasonName,
    int NumberOfRounds,
    CompetitionType CompetitionType);
