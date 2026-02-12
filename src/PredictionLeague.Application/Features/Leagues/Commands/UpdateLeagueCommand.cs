using MediatR;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public record UpdateLeagueCommand(
    int Id,
    string Name,
    decimal Price,
    DateTime EntryDeadlineUtc,
    int PointsForExactScore,
    int PointsForCorrectResult,
    string UserId) : IRequest;