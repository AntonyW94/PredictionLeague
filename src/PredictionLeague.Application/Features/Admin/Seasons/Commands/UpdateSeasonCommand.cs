using MediatR;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public record UpdateSeasonCommand(
    int Id, 
    string Name,
    DateTime StartDateUtc, 
    DateTime EndDateUtc, 
    bool IsActive,
    int NumberOfRounds,
    int? ApiLeagueId) : IRequest;