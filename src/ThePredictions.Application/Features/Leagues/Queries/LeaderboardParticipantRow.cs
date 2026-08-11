using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// An approved member of a league, for leaderboards that show no rank-change arrow and so need no cached
/// position.
/// </summary>
/// <remarks>
/// Separate from <see cref="LeaderboardMemberRow"/> rather than reusing it with a null snapshot: a field that is
/// always null for one consumer invites the question of whether that is deliberate or an oversight, and it would
/// force the adapter to select a <c>CAST(NULL AS int)</c> column to satisfy the mapping. Two small honest types
/// beat one type with a hole in it.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeaderboardParticipantRow(string UserId, string FirstName, string LastName);
