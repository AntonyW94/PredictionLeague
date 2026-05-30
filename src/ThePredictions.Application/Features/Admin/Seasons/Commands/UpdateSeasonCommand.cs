using MediatR;
using ThePredictions.Contracts.Admin.Seasons;

namespace ThePredictions.Application.Features.Admin.Seasons.Commands;

public record UpdateSeasonCommand(
    int Id,
    string Name,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    bool IsActive,
    int NumberOfRounds,
    int CompetitionId,
    List<TournamentRoundMappingDto> TournamentRoundMappings) : IRequest;