using MediatR;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>
/// Everybody with a prize to be told about, grouped so each of them gets one email listing all of theirs.
/// </summary>
public class GetPrizeWinnersForRoundQueryHandler(
    IRoundHeaderQuery roundHeaderQuery,
    IPrizeWinnersQuery prizeWinnersQuery) : IRequestHandler<GetPrizeWinnersForRoundQuery, IReadOnlyList<PrizeWinner>>
{
    public async Task<IReadOnlyList<PrizeWinner>> Handle(
        GetPrizeWinnersForRoundQuery request,
        CancellationToken cancellationToken)
    {
        var round = await roundHeaderQuery.ExecuteAsync(request.RoundId, cancellationToken);

        if (round is null)
            return [];

        var data = await prizeWinnersQuery.ExecuteAsync(request.RoundId, cancellationToken);
        var roundName = Round.DisplayNameOrDefault(round.DisplayName, round.RoundNumber);

        return data.Winnings
            .Where(IsWorthTellingThemAbout)
            .GroupBy(winning => winning.UserId)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => ToWinner(group, roundName, data))
            .ToList();
    }

    /// <summary>
    /// Whether this is a prize at all.
    /// </summary>
    /// <remarks>
    /// A winning of nothing is recorded when somebody placed in a category that pays out no money at that position. Emailing
    /// them about it would be telling them they had won zero pounds.
    /// </remarks>
    private static bool IsWorthTellingThemAbout(PrizeWinningRow winning) => winning.Amount > 0m;

    private static PrizeWinner ToWinner(
        IGrouping<string, PrizeWinningRow> winnings,
        string roundName,
        PrizeWinnersData data)
    {
        var first = winnings.First();

        return new PrizeWinner(
            first.UserId,
            first.Email,
            first.FirstName,
            roundName,
            winnings
                .OrderBy(winning => winning.LeagueName, StringComparer.InvariantCultureIgnoreCase)
                .Select(winning => ToWonPrize(winning, data))
                .ToList());
    }

    private static WonPrize ToWonPrize(PrizeWinningRow winning, PrizeWinnersData data) =>
        new(winning.LeagueId,
            winning.LeagueName,
            winning.LeaguePrizeSettingId,
            winning.PrizeType,
            winning.PrizeDescription,
            winning.Rank,
            winning.Stage,
            winning.Amount,
            winning.RoundNumber,
            winning.Month,
            PrizeRoundName(winning, data),
            AlreadyNotified(winning, data));

    /// <summary>
    /// The name of the round a round prize was won in, or nothing for a prize that is not about one round.
    /// </summary>
    private static string? PrizeRoundName(PrizeWinningRow winning, PrizeWinnersData data)
    {
        if (winning.RoundNumber is not { } roundNumber)
            return null;

        var round = data.SeasonRounds.SingleOrDefault(candidate => candidate.RoundNumber == roundNumber);

        return round is null ? null : Round.DisplayNameOrDefault(round.DisplayName, round.RoundNumber);
    }

    /// <summary>
    /// Whether this exact prize has already been emailed about.
    /// </summary>
    /// <remarks>
    /// The same prize slot pays out repeatedly - a round prize once a round, a monthly prize once a month - so the sent-log has
    /// to be matched on the scope as well as the slot. Two prizes with no round and no month are the same prize; in SQL that
    /// needed <c>ISNULL(..., -1)</c> on both sides, because there two nulls are never equal.
    /// </remarks>
    private static bool AlreadyNotified(PrizeWinningRow winning, PrizeWinnersData data) =>
        data.Notifications.Any(notification =>
            notification.UserId == winning.UserId
            && notification.LeaguePrizeSettingId == winning.LeaguePrizeSettingId
            && notification.RoundNumber == winning.RoundNumber
            && notification.Month == winning.Month);
}
