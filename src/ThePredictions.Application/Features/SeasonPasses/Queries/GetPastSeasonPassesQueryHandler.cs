using MediatR;
using ThePredictions.Contracts.SeasonPasses;
using ThePredictions.Domain.Common;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

/// <summary>The seasons this player missed: ones that ran without them, and can no longer be joined.</summary>
public class GetPastSeasonPassesQueryHandler(
    ISeasonPassPagesQuery seasonPassPagesQuery,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetPastSeasonPassesQuery, IEnumerable<PastSeasonPassDto>>
{
    public async Task<IEnumerable<PastSeasonPassDto>> Handle(GetPastSeasonPassesQuery request, CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;

        var data = await seasonPassPagesQuery.ExecuteAsync(request.UserId, cancellationToken);

        return SeasonPassAvailability.NewestFirst(data.Seasons)
            .Where(season => WasMissed(data, season, utcNow))
            .Select(season => new PastSeasonPassDto(
                season.Id,
                season.Name,
                season.CompetitionLogoUrl,
                SeasonPassAvailability.PlayerCount(data, season.Id)))
            .ToList();
    }

    /// <summary>
    /// Whether this season is one they missed.
    /// </summary>
    /// <remarks>
    /// The complement of the available-passes rule, and until both were written out that was not visible: same season, same
    /// "not already held", and then entry closed everywhere rather than open somewhere. The extra condition is that the
    /// season had leagues at all - a season set up and never used was not missed, it never happened.
    /// </remarks>
    private static bool WasMissed(SeasonPassPagesData data, SeasonPassSeasonRow season, DateTime utcNow) =>
        season.IsActive
        && !SeasonPassAvailability.AlreadyHeld(data, season.Id)
        && SeasonPassAvailability.HasAnyLeague(data, season.Id)
        && !SeasonPassAvailability.IsEntryOpen(data, season.Id, utcNow);
}
