using MediatR;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public record UpdateSeasonCommand(
    int Id, 
    string Name,
    DateTime StartDate, 
    DateTime EndDate, 
    bool IsActive,
    int NumberOfRounds) : IRequest;