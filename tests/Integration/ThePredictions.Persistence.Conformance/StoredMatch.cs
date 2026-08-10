using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.Conformance;

/// <summary>
/// A match as the database actually holds it. Status is the enum rather than the stored string, so a test
/// does not have to know how a given adapter persists it.
/// </summary>
public sealed record StoredMatch(
    int Id,
    int RoundId,
    int? HomeTeamId,
    int? AwayTeamId,
    DateTime MatchDateTimeUtc,
    DateTime? CustomLockTimeUtc,
    MatchStatus Status,
    int? ExternalId,
    int? MatchNumber);
