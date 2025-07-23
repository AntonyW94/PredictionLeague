using MediatR;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public record CreateLeagueCommand(
    string Name,
    int SeasonId,
    decimal Price,
    string CreatingUserId,
    DateTime EntryDeadline
) : IRequest<LeagueDto>, ITransactionalRequest;