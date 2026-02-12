using MediatR;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public record CreateSeasonCommand(
    string Name, 
    DateTime StartDateUtc, 
    DateTime EndDateUtc,
    string CreatorId,
    bool IsActive,
    int NumberOfRounds,
    int? ApiLeagueId) : IRequest<SeasonDto>, ITransactionalRequest;