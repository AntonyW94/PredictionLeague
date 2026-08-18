using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>
/// What to call a prize somebody won, on the administrator's list of accounts.
/// </summary>
/// <remarks>
/// Deliberately not <c>PrizeNotificationFormatter</c>, which names prizes for the "Prize Won" email. That one needs the
/// winner's placing to say "Overall - 2nd", and a placing is not stored on a winning - the email composes its prizes from
/// the round it has just processed, where the rank is still in hand. Reading a winning back months later, the rank is
/// gone. Rather than invent one or carry a nullable rank through a read that has no use for it, this states only what the
/// row can support.
///
/// Every case falls back rather than throwing. The prize type, stage, round number and month are all stored values, and a
/// single unexpected row must not take the whole screen down - an administrator looking at a list of accounts is often
/// looking at it precisely because something is wrong with one of them.
/// </remarks>
public static class UserPrizeTitle
{
    public static string Of(PrizeType prizeType, string? stage, int? roundNumber, int? month) => prizeType switch
    {
        PrizeType.Overall => "Overall winner",
        PrizeType.MostExactScores => "Most exact scores",
        PrizeType.Round => RoundTitle(roundNumber),
        PrizeType.Monthly => MonthlyTitle(month),
        PrizeType.Stages => StageTitle(stage),
        _ => "Prize"
    };

    /// <summary>
    /// The round number, not its name. Named rounds read better, but the name lives on the round and a winning points at
    /// a prize setting instead, which stores only the number.
    /// </summary>
    private static string RoundTitle(int? roundNumber) =>
        roundNumber is null
            ? "Round winner"
            : $"Round {roundNumber} winner";

    /// <summary>
    /// <c>MonthName</c> returns nothing for a month outside 1-12, which the column allows, so the generic title covers
    /// both a missing month and an impossible one.
    /// </summary>
    private static string MonthlyTitle(int? month) =>
        MonthName.Of(month) is { } name
            ? $"{name} winner"
            : "Monthly winner";

    private static string StageTitle(string? stage) =>
        string.IsNullOrWhiteSpace(stage)
            ? "Stage winner"
            : $"{stage} winner";
}
