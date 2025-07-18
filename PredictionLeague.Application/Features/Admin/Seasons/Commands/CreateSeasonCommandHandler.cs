using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public class CreateSeasonCommandHandler : IRequestHandler<CreateSeasonCommand>
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly ILeagueRepository _leagueRepository;

    public CreateSeasonCommandHandler(ISeasonRepository seasonRepository, ILeagueRepository leagueRepository)
    {
        _seasonRepository = seasonRepository;
        _leagueRepository = leagueRepository;
    }

    public async Task Handle(CreateSeasonCommand request, CancellationToken cancellationToken)
    {
        var season = new Season
        {
            Name = request.Name,
            IsActive = true
        };

        season.SetDates(request.StartDate, request.EndDate);

        await _seasonRepository.AddAsync(season);

        var publicLeague = new League
        {
            Name = $"{season.Name} Official",
            SeasonId = season.Id,
            AdministratorUserId = request.CreatorId,
            EntryCode = null
        };
        await _leagueRepository.CreateAsync(publicLeague);

        var leagueMember = new LeagueMember
        {
            LeagueId = publicLeague.Id,
            UserId = request.CreatorId,
            JoinedAt = DateTime.UtcNow,
            Status = LeagueMemberStatus.Approved
        };
        await _leagueRepository.AddMemberAsync(leagueMember);
    }
}