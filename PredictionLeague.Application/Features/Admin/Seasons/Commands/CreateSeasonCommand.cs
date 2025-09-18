using MediatR;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public record CreateSeasonCommand(
    string Name, 
    DateTime StartDate, 
    DateTime EndDate,
    string CreatorId,
    bool IsActive,
    int NumberOfRounds,
    int? ApiLeagueId) : IRequest<SeasonDto>, ITransactionalRequest;