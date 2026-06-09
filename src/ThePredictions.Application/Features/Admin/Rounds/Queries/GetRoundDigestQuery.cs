using MediatR;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>
/// Builds the round-results digest data for every user who predicted in the given round,
/// grouped by user with one entry per league they belong to in the round's season.
/// </summary>
public record GetRoundDigestQuery(int RoundId) : IRequest<IReadOnlyList<UserRoundDigest>>;
