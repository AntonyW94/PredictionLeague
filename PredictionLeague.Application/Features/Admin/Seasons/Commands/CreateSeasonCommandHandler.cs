using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Admin.Seasons;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public class CreateSeasonCommandHandler : IRequestHandler<CreateSeasonCommand, SeasonDto>
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly ILeagueRepository _leagueRepository;

    public CreateSeasonCommandHandler(ISeasonRepository seasonRepository, ILeagueRepository leagueRepository)
    {
        _seasonRepository = seasonRepository;
        _leagueRepository = leagueRepository;
    }

    public async Task<SeasonDto> Handle(CreateSeasonCommand request, CancellationToken cancellationToken)
    {
        var season = Season.Create(
            request.Name,
            request.StartDate,
            request.EndDate,
            request.IsActive);

        var createdSeason = await _seasonRepository.CreateAsync(season, cancellationToken);

        var publicLeague = League.Create(
            createdSeason.Id,
            $"Official {request.Name} League",
            request.CreatorId, 
            null,
            null
        ); 
        
        publicLeague.AddMember(request.CreatorId);

        await _leagueRepository.CreateAsync(publicLeague, cancellationToken);
        
        var adminMember = publicLeague.Members.First();
        adminMember.Approve();
        
        await _leagueRepository.UpdateMemberStatusAsync(publicLeague.Id, request.CreatorId, LeagueMemberStatus.Approved, cancellationToken);

        return new SeasonDto(
            createdSeason.Id,
            createdSeason.Name,
            createdSeason.StartDate,
            createdSeason.EndDate,
            createdSeason.IsActive,
            0
        );
    }
}