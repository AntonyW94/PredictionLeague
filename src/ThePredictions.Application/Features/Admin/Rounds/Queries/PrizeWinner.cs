namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>
/// One winner's complete set of prizes for a round's season, ready to render into a single grouped
/// "Prize Won" email: the recipient's details plus a row per prize they have won.
/// </summary>
public record PrizeWinner(
    string UserId,
    string Email,
    string FirstName,
    string RoundName,
    IReadOnlyList<WonPrize> Prizes);
