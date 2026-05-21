using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class SetLeagueArchivedCommandHandler(ILeagueMemberRepository leagueMemberRepository) : IRequestHandler<SetLeagueArchivedCommand>
{
    public async Task Handle(SetLeagueArchivedCommand request, CancellationToken cancellationToken)
    {
        var member = await leagueMemberRepository.GetAsync(request.LeagueId, request.UserId, cancellationToken);
        Guard.Against.EntityNotFound(request.UserId, member, "League Membership");

        if (request.IsArchived)
            member.Archive();
        else
            member.Unarchive();

        await leagueMemberRepository.UpdateAsync(member, cancellationToken);
    }
}
