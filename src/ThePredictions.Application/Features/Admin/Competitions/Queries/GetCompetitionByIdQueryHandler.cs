using MediatR;
using ThePredictions.Contracts.Admin.Competitions;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Admin.Competitions.Queries;

/// <summary>One competition, for the administrator's edit screen.</summary>
public class GetCompetitionByIdQueryHandler(ICompetitionsQuery competitionsQuery)
    : IRequestHandler<GetCompetitionByIdQuery, CompetitionDto>
{
    public async Task<CompetitionDto> Handle(GetCompetitionByIdQuery request, CancellationToken cancellationToken)
    {
        var competitions = await competitionsQuery.ExecuteAsync(cancellationToken);

        var competition = competitions.SingleOrDefault(candidate => candidate.Id == request.Id)
                          ?? throw new EntityNotFoundException("Competition", request.Id);

        return CompetitionMapping.ToDto(competition);
    }
}
