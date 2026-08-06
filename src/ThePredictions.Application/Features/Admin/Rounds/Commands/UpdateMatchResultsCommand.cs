using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Contracts.Admin.Matches;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record UpdateMatchResultsCommand(
    int RoundId,
    List<MatchResultDto> Matches) : IRequest, ITransactionalRequest;
