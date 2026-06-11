using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.External.Tasks.Queries;

/// <summary>
/// One frozen prize setting for the welcome email. Recurring settings (Round/Monthly) store the
/// per-event amount once; the formatter multiplies them by their occurrence count for the pot total.
/// </summary>
public record LeagueWelcomePrize(
    PrizeType PrizeType,
    int Rank,
    string? Stage,
    decimal Amount);
