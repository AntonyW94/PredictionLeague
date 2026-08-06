using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Leagues.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class GetLeaguePrizeBreakdownQueryHandler(
    IPrizeEvaluationInputsReader inputsReader,
    ILeagueMembershipService membershipService,
    IPrizeEvaluator evaluator) : IRequestHandler<GetLeaguePrizeBreakdownQuery, PrizeBreakdownDto>
{
    public async Task<PrizeBreakdownDto> Handle(GetLeaguePrizeBreakdownQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        var inputs = await inputsReader.LoadAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, inputs, "League");

        if (!inputs.HasScheme)
            return new PrizeBreakdownDto { EntrantCount = inputs.EntrantCount };

        return evaluator.Evaluate(inputs.ToEvaluationRequest(inputs.EntrantCount));
    }
}
