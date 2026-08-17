using Ardalis.GuardClauses;
using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Leagues.Commands;

/// <summary>
/// Places a player in a league on a system administrator's behalf, approved immediately.
/// </summary>
/// <remarks>
/// This exists for the player who has paid for the season but could not finish joining a league before its entry
/// deadline. It is the one path that admits a member past that deadline, which is why it is administrator-only.
///
/// The Season Pass check is <b>not</b> waived. An administrator is overriding when somebody joined, not whether they are
/// entitled to take part in the season at all, and a member with no pass would be in a league for a season they cannot
/// predict in.
/// </remarks>
public class AddLeagueMemberCommandHandler(
    ILeagueRepository leagueRepository,
    ILeagueStatsRepository leagueStatsRepository,
    ISeasonAccessService seasonAccessService,
    ICurrentUserService currentUserService,
    IMediator mediator,
    IDateTimeProvider dateTimeProvider,
    ILogger<AddLeagueMemberCommandHandler> logger) : IRequestHandler<AddLeagueMemberCommand>
{
    public async Task Handle(AddLeagueMemberCommand request, CancellationToken cancellationToken)
    {
        currentUserService.EnsureAdministrator();

        var league = await leagueRepository.GetByIdAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, league, "League");

        await seasonAccessService.EnsureCanParticipateAsync(request.UserId, league.SeasonId, cancellationToken);

        league.AddMemberAsAdministrator(request.UserId, dateTimeProvider);

        await leagueRepository.UpdateAsync(league, cancellationToken);

        // They go in approved, so they are ranked straight away - which moves every other member's cached rank, not just
        // their own. Without this the league's tiles stay wrong until the next results update.
        await leagueStatsRepository.RefreshLeagueAsync(league.Id, cancellationToken);

        logger.LogInformation(
            "User (ID: {UserId}) was added to League (ID: {LeagueId}) by an administrator",
            request.UserId,
            league.Id);

        // The same email an approval sends, because from the player's side this is the same event: they can now take part.
        await mediator.Send(
            new NotifyMemberOfLeagueApprovalCommand(request.UserId, league.Id, league.Name, league.SeasonId),
            cancellationToken);
    }
}
