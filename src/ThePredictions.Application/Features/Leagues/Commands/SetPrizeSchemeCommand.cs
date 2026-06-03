using MediatR;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Contracts.Prizes;

namespace ThePredictions.Application.Features.Leagues.Commands;

/// <summary>
/// Sets a league's prize scheme. Write-once: a league admin may set it while it is unset (new
/// leagues at creation, or once on an existing schemeless league); thereafter only a site admin
/// can override it to correct a mistake.
/// </summary>
public record SetPrizeSchemeCommand(
    int LeagueId,
    string UserId,
    PrizeSchemeRequest Scheme
) : IRequest, ITransactionalRequest;
