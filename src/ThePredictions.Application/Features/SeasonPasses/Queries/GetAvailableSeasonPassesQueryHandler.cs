using MediatR;
using ThePredictions.Contracts.SeasonPasses;
using ThePredictions.Domain.Common;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

/// <summary>The seasons this player could still take a pass out for.</summary>
public class GetAvailableSeasonPassesQueryHandler(
    ISeasonPassPagesQuery seasonPassPagesQuery,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetAvailableSeasonPassesQuery, IEnumerable<AvailableSeasonPassDto>>
{
    public async Task<IEnumerable<AvailableSeasonPassDto>> Handle(GetAvailableSeasonPassesQuery request, CancellationToken cancellationToken)
    {
        // Read the clock once: whether entry is still open and when it next closes are decided against the same instant.
        var utcNow = dateTimeProvider.UtcNow;

        var data = await seasonPassPagesQuery.ExecuteAsync(request.UserId, cancellationToken);

        return SeasonPassAvailability.NewestFirst(data.Seasons)
            .Where(season => IsOnOffer(data, season, utcNow))
            .Select(season => ToDto(data, season, utcNow))
            .ToList();
    }

    /// <summary>
    /// Whether to offer this season: one that is running, that they do not already hold, and that still has a league they
    /// could join.
    /// </summary>
    /// <remarks>
    /// The last condition is the whole point - a pass buys entry to a league, so a season whose leagues have all closed is
    /// not worth selling. It is also exactly what the past-passes page tests for the opposite of.
    /// </remarks>
    private static bool IsOnOffer(SeasonPassPagesData data, SeasonPassSeasonRow season, DateTime utcNow) =>
        season.IsActive
        && !SeasonPassAvailability.AlreadyHeld(data, season.Id)
        && SeasonPassAvailability.IsEntryOpen(data, season.Id, utcNow);

    private static AvailableSeasonPassDto ToDto(SeasonPassPagesData data, SeasonPassSeasonRow season, DateTime utcNow) =>
        new(season.Id,
            season.Name,
            season.CompetitionLogoUrl,
            SeasonPassAvailability.RequiresPayment(season),
            season.StandardPrice,
            season.PremiumPrice,
            SeasonPassAvailability.IsTrialEligible(data),
            SeasonPassAvailability.PlayerCount(data, season.Id),
            SeasonPassAvailability.NextEntryDeadline(data, season.Id, utcNow));
}
