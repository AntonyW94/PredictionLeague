using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Contracts.Boosts;

namespace ThePredictions.Application.Features.Boosts.Commands;

/// <summary>
/// Sets which boosts a league offers (and their season caps / windows). Write-once, mirroring the
/// prize scheme: a league admin sets it while unset; thereafter only a site admin can override it.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record SetLeagueBoostRulesCommand(
    int LeagueId,
    string UserId,
    List<LeagueBoostSelectionDto> Selections
) : IRequest, ITransactionalRequest;
