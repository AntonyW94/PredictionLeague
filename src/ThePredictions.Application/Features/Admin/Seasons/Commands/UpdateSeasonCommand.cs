using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Admin.Seasons;

namespace ThePredictions.Application.Features.Admin.Seasons.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record UpdateSeasonCommand(
    int Id,
    string Name,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    bool IsActive,
    int NumberOfRounds,
    int CompetitionId,
    decimal? PassStandardPrice,
    List<TournamentRoundMappingDto> TournamentRoundMappings) : IRequest;
