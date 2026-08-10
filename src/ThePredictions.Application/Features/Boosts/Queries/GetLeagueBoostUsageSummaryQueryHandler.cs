using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Boosts;
using ThePredictions.Domain.Common;

namespace ThePredictions.Application.Features.Boosts.Queries;

/// <summary>
/// The league's boost-usage table: who has played what, and how many uses each player has left.
///
/// No longer carries SQL, and therefore no longer carries <c>[ExcludeFromCodeCoverage]</c>. What is left is
/// the sequence that matters: authorise, read, <b>censor</b>, shape. The censoring step is the reason this
/// handler is worth measuring - a fairness rule that the handler forgot to apply would be a silent leak, and
/// that is a failure mode the SQL version could not have, because the predicate was inside the read itself.
/// <c>BoostUsageSecrecyTests</c> guards exactly that end to end.
/// </summary>
public class GetLeagueBoostUsageSummaryQueryHandler(
    ILeagueBoostUsageQuery usageQuery,
    ILeagueMembershipService membershipService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetLeagueBoostUsageSummaryQuery, List<BoostUsageSummaryDto>>
{
    public async Task<List<BoostUsageSummaryDto>> Handle(
        GetLeagueBoostUsageSummaryQuery request,
        CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(
            request.LeagueId, request.CurrentUserId, cancellationToken);

        var data = await usageQuery.ExecuteAsync(request.LeagueId, cancellationToken);

        if (data == null || data.BoostRules.Count == 0)
            return [];

        // Censor before shaping. The query returns every usage in the league; what this player may see is
        // decided here, against the injected clock.
        var visibleUsages = BoostUsageVisibility.VisibleTo(
            data.Usages, request.CurrentUserId, dateTimeProvider.UtcNow);

        return BoostUsageSummaryBuilder.Build(
            data.BoostRules,
            data.Windows,
            data.Members,
            visibleUsages,
            data.RoundRange,
            data.InProgressRoundNumber,
            data.LastCompletedRoundNumber,
            request.CurrentUserId);
    }
}
