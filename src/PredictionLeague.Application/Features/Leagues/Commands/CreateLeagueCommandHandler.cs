using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common;
using PredictionLeague.Domain.Common.Guards;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class CreateLeagueCommandHandler : IRequestHandler<CreateLeagueCommand, LeagueDto>
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateLeagueCommandHandler(ILeagueRepository leagueRepository, ISeasonRepository seasonRepository, IDateTimeProvider dateTimeProvider)
    {
        _leagueRepository = leagueRepository;
        _seasonRepository = seasonRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<LeagueDto> Handle(CreateLeagueCommand request, CancellationToken cancellationToken)
    {
        var season = await _seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(request.SeasonId, season, "Season");

        var league = League.Create(
             request.SeasonId,
             request.Name,
             request.CreatingUserId,
             request.EntryDeadlineUtc,
             request.PointsForExactScore,
             request.PointsForCorrectResult,
             request.Price,
             season,
             _dateTimeProvider
         );

        string entryCode;
        do
        {
            entryCode = GenerateRandomEntryCode();
        } while (await _leagueRepository.GetByEntryCodeAsync(entryCode, cancellationToken) != null);

        league.SetEntryCode(entryCode);

        var createdLeague = await _leagueRepository.CreateAsync(league, cancellationToken);

        return new LeagueDto(
            createdLeague.Id,
            createdLeague.Name,
            season.Name,
            1,
            createdLeague.Price,
            createdLeague.EntryCode ?? "Public",
            createdLeague.EntryDeadlineUtc,
            createdLeague.PointsForExactScore,
            createdLeague.PointsForCorrectResult
        );
    }

    private static string GenerateRandomEntryCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}