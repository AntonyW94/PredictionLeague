using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Contracts.Admin.Seasons;

namespace ThePredictions.Application.Features.Admin.Seasons.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
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
