using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Contracts.Admin.Matches;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record UpdateRoundCommand(
    int RoundId, 
    int RoundNumber,
    string ApiRoundName,
    DateTime StartDateUtc, 
    DateTime DeadlineUtc, 
    RoundStatus Status,
    List<UpdateMatchRequest> Matches) : IRequest, ITransactionalRequest;
