using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Rounds.Queries;

/// <summary>
/// A player taking part in the round. Name parts rather than a formatted name: formatting is
/// <see cref="Domain.Services.PlayerDisplayName"/>'s rule, not the database's.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record RoundParticipantRow(
    string UserId,
    string FirstName,
    string LastName,
    string Email,
    DateTime? LastRemindedUtc);
