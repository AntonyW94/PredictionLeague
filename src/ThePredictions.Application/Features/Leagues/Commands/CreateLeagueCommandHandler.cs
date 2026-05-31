using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class CreateLeagueCommandHandler(ILeagueRepository leagueRepository, ISeasonRepository seasonRepository, ISeasonAccessService seasonAccessService, IDateTimeProvider dateTimeProvider) : IRequestHandler<CreateLeagueCommand, LeagueDto>
{
    public async Task<LeagueDto> Handle(CreateLeagueCommand request, CancellationToken cancellationToken)
    {
        var season = await seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(request.SeasonId, season, "Season");

        await seasonAccessService.EnsureCanParticipateAsync(request.CreatingUserId, request.SeasonId, cancellationToken);

        var league = League.Create(
             request.SeasonId,
             request.Name,
             request.CreatingUserId,
             request.EntryDeadlineUtc,
             request.PointsForExactScore,
             request.PointsForCorrectResult,
             request.Price,
             season,
             dateTimeProvider
         );

        string entryCode;
        do
        {
            entryCode = GenerateRandomEntryCode();
        } while (await leagueRepository.GetByEntryCodeAsync(entryCode, cancellationToken) != null);

        league.SetEntryCode(entryCode);

        var createdLeague = await leagueRepository.CreateAsync(league, cancellationToken);

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