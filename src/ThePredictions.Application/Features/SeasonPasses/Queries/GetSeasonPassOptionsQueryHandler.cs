using MediatR;
using ThePredictions.Contracts.SeasonPasses;
using ThePredictions.Domain.Common;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

/// <summary>What one season offers this player, for the page where they take a pass out.</summary>
public class GetSeasonPassOptionsQueryHandler(
    ISeasonPassPagesQuery seasonPassPagesQuery,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetSeasonPassOptionsQuery, SeasonPassOptionsDto?>
{
    public async Task<SeasonPassOptionsDto?> Handle(GetSeasonPassOptionsQuery request, CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;

        var data = await seasonPassPagesQuery.ExecuteAsync(request.UserId, cancellationToken);

        var season = data.Seasons.SingleOrDefault(candidate => candidate.Id == request.SeasonId);

        if (season is null)
            return null;

        // Deliberately not filtered by whether it is on offer: this page reports what the season's state is, including
        // that the player already holds it or that entry has closed, so the screen can say so.
        return new SeasonPassOptionsDto(
            season.Id,
            season.Name,
            season.CompetitionLogoUrl,
            season.CompetitionDescription,
            SeasonPassAvailability.RequiresPayment(season),
            season.StandardPrice,
            season.PremiumPrice,
            SeasonPassAvailability.IsTrialEligible(data),
            SeasonPassAvailability.AlreadyHeld(data, season.Id),
            SeasonPassAvailability.IsEntryOpen(data, season.Id, utcNow),
            SeasonPassAvailability.PlayerCount(data, season.Id),
            SeasonPassAvailability.NextEntryDeadline(data, season.Id, utcNow));
    }
}
