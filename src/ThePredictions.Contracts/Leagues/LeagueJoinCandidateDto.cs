using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

/// <summary>
/// Somebody an administrator could place in a league: they hold a Season Pass for the league's season and have no
/// membership of it yet. The email is carried because two players can share a display name and an administrator adding
/// the wrong one is not something the league can undo.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record LeagueJoinCandidateDto(
    string UserId,
    string FullName,
    string Email);
