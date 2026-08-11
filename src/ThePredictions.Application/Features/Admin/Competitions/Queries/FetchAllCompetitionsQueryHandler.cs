using MediatR;
using ThePredictions.Contracts.Admin.Competitions;

namespace ThePredictions.Application.Features.Admin.Competitions.Queries;

/// <summary>The administrator's list of competitions.</summary>
public class FetchAllCompetitionsQueryHandler(ICompetitionsQuery competitionsQuery)
    : IRequestHandler<FetchAllCompetitionsQuery, IEnumerable<CompetitionDto>>
{
    public async Task<IEnumerable<CompetitionDto>> Handle(FetchAllCompetitionsQuery request, CancellationToken cancellationToken)
    {
        var competitions = await competitionsQuery.ExecuteAsync(cancellationToken);

        return CompetitionMapping.InNameOrder(competitions).Select(CompetitionMapping.ToDto).ToList();
    }
}
