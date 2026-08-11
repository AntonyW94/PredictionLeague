using MediatR;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>One round and its fixtures, for the administrator's editor.</summary>
public class GetRoundByIdQueryHandler(IAdminRoundQuery roundQuery, IRoundMatchesQuery roundMatchesQuery)
    : IRequestHandler<GetRoundByIdQuery, RoundDetailsDto>
{
    public async Task<RoundDetailsDto> Handle(GetRoundByIdQuery request, CancellationToken cancellationToken)
    {
        var round = await roundQuery.ExecuteAsync(request.Id, cancellationToken)
                    ?? throw new EntityNotFoundException("Round", request.Id);

        var matches = await roundMatchesQuery.ExecuteAsync(request.Id, cancellationToken);

        return new RoundDetailsDto
        {
            Round = AdminRoundMapping.ToDto(round),

            // Every fixture, including any that have been called off - this is the screen an administrator uses to put
            // a postponed fixture back, so hiding it would hide the thing they came to fix.
            Matches = RoundMatches.InKickOffOrder(matches)
                .Select(RoundMatches.ToDto)
                .ToList()
        };
    }
}
