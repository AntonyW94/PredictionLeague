using MediatR;

namespace ThePredictions.Application.Features.Sharing.Queries;

/// <summary>
/// Renders the calling user's predictions for a round as a shareable PNG. Returns null when the
/// round does not exist or the user has not predicted any of its confirmed matches.
/// </summary>
public record GetRoundShareCardImageQuery(int RoundId, string UserId) : IRequest<byte[]?>;
