using MediatR;

namespace ThePredictions.Application.Features.Sharing.Queries;

/// <summary>
/// Renders the calling user's predictions for a round as a shareable PNG. Returns null when the
/// round does not exist or the user has not predicted any of its confirmed matches.
/// </summary>
/// <remarks>
/// <paramref name="Theme"/> is the theme the client is currently showing ("light"/"dark"); when
/// null the handler falls back to the user's saved <c>PreferredTheme</c>.
/// </remarks>
public record GetRoundShareCardImageQuery(int RoundId, string UserId, string? Theme) : IRequest<byte[]?>;
