using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>One round of the season, as the digest needs to name it and date it.</summary>
/// <remarks>
/// The name arrives exactly as stored. Falling back to "Round 12" when it is blank is
/// <c>Round.DisplayNameOrDefault</c>, and the statement this replaces was the one read on the site that skipped that
/// guard: every round in the database happens to be named, so this was a gap waiting for the first one that is not.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record RoundDigestRoundRow(int Id, int RoundNumber, string DisplayName, DateTime DeadlineUtc);
