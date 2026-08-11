using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Rounds;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Rounds.Queries;

/// <summary>
/// Prediction-completion overview for a round: who is up to date, who is missing what.
///
/// This handler used to hold three SQL statements sharing a <c>PredictableMatchPredicate</c> constant, which
/// carried a comment asking whoever changed it to change the identical predicate in
/// <c>ReminderService.GetUsersMissingPredictionsAsync</c> too. Nothing enforced that, and the rule was in fact
/// already in the domain - <c>Match.AreTeamsConfirmed</c> and <c>Match.IsPredictionLocked</c> both existed and
/// were both tested. Only the composition was missing, so each call site rewrote it in T-SQL.
///
/// Now the port returns the round, its fixtures, its participants and their predictions, and every rule is
/// applied here: <see cref="Domain.Models.Match.IsOpenForPrediction"/> decides what still counts,
/// <c>Round.GetDisplayNameOrDefault</c> names the round, and <see cref="PlayerDisplayName"/> names the players.
/// </summary>
public class GetRoundCompletionQueryHandler(
    IRoundCompletionQuery completionQuery,
    ILeagueMembershipService membershipService,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetRoundCompletionQuery, RoundCompletionDto>
{
    public async Task<RoundCompletionDto> Handle(GetRoundCompletionQuery request, CancellationToken cancellationToken)
    {
        var canSendReminders = await AuthoriseAsync(request, cancellationToken);

        var data = await completionQuery.ExecuteAsync(request.RoundId, request.LeagueId, cancellationToken)
                   ?? throw new EntityNotFoundException("Round", request.RoundId);

        var nowUtc = dateTimeProvider.UtcNow;

        // One rule, one place. A fixture counts while a player can still act on it.
        var openFixtures = data.Round.Matches
            .Where(m => m.IsOpenForPrediction(nowUtc, data.Round.DeadlineUtc))
            .ToList();

        var openMatchIds = openFixtures.Select(m => m.Id).ToHashSet();

        var predictedMatchIdsByUser = data.Predictions
            .Where(p => openMatchIds.Contains(p.MatchId))
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.MatchId).ToHashSet());

        var players = data.Participants
            .Select(participant => BuildPlayer(participant, openFixtures, data.TeamNames, predictedMatchIdsByUser))
            .OrderByDescending(p => p.IsPartial)
            .ThenByDescending(p => p.HasEnteredNothing)
            .ThenBy(p => p.PlayerName)
            .ToList();

        return new RoundCompletionDto(
            request.RoundId,
            data.Round.GetDisplayNameOrDefault(),
            data.Round.DeadlineUtc,
            // "Passed" for chase purposes means nothing is left to predict, which is not the same as the
            // clock having run out: a combined round stays open while its later batch is unlocked, even
            // though the deadline that locked the earlier batch has gone.
            DeadlinePassed: openFixtures.Count == 0,
            canSendReminders,
            openFixtures.Count,
            players);
    }

    private static RoundCompletionPlayerDto BuildPlayer(
        RoundParticipantRow participant,
        IReadOnlyList<Match> openFixtures,
        IReadOnlyDictionary<int, RoundFixtureTeams> teamNames,
        IReadOnlyDictionary<string, HashSet<int>> predictedMatchIdsByUser)
    {
        var predicted = predictedMatchIdsByUser.TryGetValue(participant.UserId, out var ids)
            ? ids
            : [];

        var missing = openFixtures
            .Where(match => !predicted.Contains(match.Id))
            .OrderBy(match => match.MatchNumber)
            .Select(match =>
            {
                teamNames.TryGetValue(match.Id, out var teams);
                return new MissingFixtureDto(
                    match.Id, match.MatchNumber,
                    teams?.HomeTeamName ?? string.Empty, teams?.AwayTeamName ?? string.Empty);
            })
            .ToList();

        return new RoundCompletionPlayerDto(
            participant.UserId,
            PlayerDisplayName.Format(participant.FirstName, participant.LastName),
            participant.Email,
            predicted.Count,
            participant.LastRemindedUtc,
            missing);
    }

    private async Task<bool> AuthoriseAsync(GetRoundCompletionQuery request, CancellationToken cancellationToken)
    {
        // Global view is admin-only; the league view is readable by any approved member, but only an
        // admin or the league owner may then send reminders.
        if (request.LeagueId == null)
        {
            if (!request.IsSiteAdmin)
                throw new UnauthorizedAccessException("Only an administrator can view round completion across all leagues.");

            return true;
        }

        await membershipService.EnsureApprovedMemberAsync(request.LeagueId.Value, request.CurrentUserId, cancellationToken);

        return request.IsSiteAdmin
               || await membershipService.IsLeagueAdministratorAsync(request.LeagueId.Value, request.CurrentUserId, cancellationToken);
    }
}
