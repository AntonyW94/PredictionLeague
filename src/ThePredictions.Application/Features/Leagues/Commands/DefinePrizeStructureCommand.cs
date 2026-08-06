using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Leagues.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record DefinePrizeStructureCommand(
    int LeagueId,
    string DefiningUserId,
    List<DefinePrizeSettingDto> PrizeSettings
) : IRequest, ITransactionalRequest;
