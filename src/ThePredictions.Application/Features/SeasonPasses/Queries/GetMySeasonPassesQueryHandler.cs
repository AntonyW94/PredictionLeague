using MediatR;
using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

/// <summary>The passes this player holds, newest first.</summary>
public class GetMySeasonPassesQueryHandler(ISeasonPassPagesQuery seasonPassPagesQuery)
    : IRequestHandler<GetMySeasonPassesQuery, IEnumerable<MySeasonPassDto>>
{
    public async Task<IEnumerable<MySeasonPassDto>> Handle(GetMySeasonPassesQuery request, CancellationToken cancellationToken)
    {
        var data = await seasonPassPagesQuery.ExecuteAsync(request.UserId, cancellationToken);

        var seasonsById = data.Seasons.ToDictionary(season => season.Id);

        return data.HeldPasses
            .OrderByDescending(pass => pass.CreatedAtUtc)
            .Select(pass => ToDto(pass, seasonsById[pass.SeasonId]))
            .ToList();
    }

    private static MySeasonPassDto ToDto(HeldSeasonPassRow pass, SeasonPassSeasonRow season) =>
        new(pass.SeasonId,
            season.Name,
            season.CompetitionLogoUrl,
            pass.Tier,
            pass.Source,
            pass.AmountPaid,
            SeasonPassAvailability.HasSmsReminders(pass),
            pass.CreatedAtUtc);
}
