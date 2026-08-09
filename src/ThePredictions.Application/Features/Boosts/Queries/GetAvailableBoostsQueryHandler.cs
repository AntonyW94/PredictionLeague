using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services.Boosts;
using ThePredictions.Contracts.Boosts;

namespace ThePredictions.Application.Features.Boosts.Queries;

public class GetAvailableBoostsQueryHandler(IBoostReadRepository boostReadRepository, IBoostService boostService)
    : IRequestHandler<GetAvailableBoostsQuery, List<BoostOptionDto>>
{
    public async Task<List<BoostOptionDto>> Handle(GetAvailableBoostsQuery request, CancellationToken cancellationToken)
    {
        var boostDefinitions = (await boostReadRepository.GetBoostDefinitionsForLeagueAsync(request.LeagueId, cancellationToken)).ToList();
        if (boostDefinitions.Count == 0)
            return new List<BoostOptionDto>();

        var tasks = boostDefinitions.Select(async d =>
        {
            var boostEligibility = await boostService.GetEligibilityAsync(request.UserId, request.LeagueId, request.RoundId, d.BoostCode, cancellationToken);
       
            return new BoostOptionDto
            {
                BoostCode = d.BoostCode,
                Name = d.Name,
                Tooltip = d.Tooltip ?? string.Empty,
                Description = d.Description ?? string.Empty,
                ImageUrl = d.ImageUrl ?? string.Empty,
                SelectedImageUrl = d.SelectedImageUrl ?? string.Empty,
                DisabledImageUrl = d.DisabledImageUrl ?? string.Empty,
                Eligibility = boostEligibility
            };
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }
}
