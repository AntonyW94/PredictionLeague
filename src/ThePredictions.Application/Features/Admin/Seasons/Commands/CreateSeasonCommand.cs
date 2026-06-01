using MediatR;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Contracts.Admin.Seasons;

namespace ThePredictions.Application.Features.Admin.Seasons.Commands;

public record CreateSeasonCommand(
    string Name,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    string CreatorId,
    bool IsActive,
    int NumberOfRounds,
    int CompetitionId,
    decimal? PassStandardPrice,
    List<TournamentRoundMappingDto> TournamentRoundMappings) : IRequest<SeasonDto>, ITransactionalRequest;