using MediatR;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public record DefinePrizeStructureCommand(
    int LeagueId,
    string DefiningUserId,
    List<DefinePrizeSettingDto> PrizeSettings
) : IRequest, ITransactionalRequest;