using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.Conformance;

/// <summary>
/// A round as the database actually holds it, for the fields the write path is responsible for.
/// </summary>
public sealed record StoredRound(
    int Id,
    int RoundNumber,
    string DisplayName,
    DateTime StartDateUtc,
    DateTime DeadlineUtc,
    RoundStatus Status,
    string? ApiRoundName);
