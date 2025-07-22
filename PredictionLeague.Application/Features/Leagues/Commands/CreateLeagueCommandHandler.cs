using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class CreateLeagueCommandHandler : IRequestHandler<CreateLeagueCommand, LeagueDto>
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ISeasonRepository _seasonRepository;

    public CreateLeagueCommandHandler(ILeagueRepository leagueRepository, ISeasonRepository seasonRepository)
    {
        _leagueRepository = leagueRepository;
        _seasonRepository = seasonRepository;
    }

    public async Task<LeagueDto> Handle(CreateLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = League.Create(
             request.SeasonId,
             request.Name,
             request.CreatingUserId,
             request.EntryCode,
             request.EntryDeadline
         );

        league.AddMember(request.CreatingUserId);

        var createdLeague = await _leagueRepository.CreateAsync(league);

        await _leagueRepository.UpdateMemberStatusAsync(
            createdLeague.Id,
            request.CreatingUserId,
            LeagueMemberStatus.Approved
        );
       
        
        var season = await _seasonRepository.GetByIdAsync(createdLeague.SeasonId);
        Guard.Against.Null(season, $"Season with ID {createdLeague.SeasonId} was not found.");

        return new LeagueDto(
            createdLeague.Id,
            createdLeague.Name,
            season.Name,
            1, 
            createdLeague.EntryCode ?? "Public"
        );
    }
}