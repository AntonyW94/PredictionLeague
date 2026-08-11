using MediatR;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Contracts.Admin.Rounds;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>The fixtures of one round as a player sees them.</summary>
public class GetMatchesForRoundQueryHandler(IRoundMatchesQuery roundMatchesQuery)
    : IRequestHandler<GetMatchesForRoundQuery, IEnumerable<MatchInRoundDto>>
{
    public async Task<IEnumerable<MatchInRoundDto>> Handle(GetMatchesForRoundQuery request, CancellationToken cancellationToken)
    {
        var matches = await roundMatchesQuery.ExecuteAsync(request.RoundId, cancellationToken);

        // A called-off fixture is left out, because a player cannot predict it and it would sit in the list as an
        // unpredicted, unscored row. The administrator's editor shows the same round with it still in.
        return RoundMatches.InKickOffOrder(matches.Where(match => !RoundMatches.IsPostponed(match)))
            .Select(RoundMatches.ToDto)
            .ToList();
    }
}
