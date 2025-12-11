using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services.Boosts;
using PredictionLeague.Contracts.Boosts;

namespace PredictionLeague.Application.Features.Predictions.Queries;

public class GetAvailableBoostsQueryHandler : IRequestHandler<GetAvailableBoostsQuery, List<BoostOptionDto>>
{
    private readonly IBoostReadRepository _boostReadRepository;
    private readonly IBoostService _boostService;

    public GetAvailableBoostsQueryHandler(IBoostReadRepository boostReadRepository, IBoostService boostService)
    {
        _boostReadRepository = boostReadRepository;
        _boostService = boostService;
    }

    public async Task<List<BoostOptionDto>> Handle(GetAvailableBoostsQuery request, CancellationToken cancellationToken)
    {
        var boostDefinitions = (await _boostReadRepository.GetBoostDefinitionsForLeagueAsync(request.LeagueId, cancellationToken)).ToList();
        if (boostDefinitions.Count == 0)
            return new List<BoostOptionDto>();

        var tasks = boostDefinitions.Select(async d =>
        {
            var boostEligibility = await _boostService.GetEligibilityAsync(request.UserId, request.LeagueId, request.RoundId, d.BoostCode, cancellationToken);
       
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