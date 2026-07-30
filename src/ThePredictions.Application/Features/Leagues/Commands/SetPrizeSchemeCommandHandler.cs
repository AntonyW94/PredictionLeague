using Ardalis.GuardClauses;
using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Constants;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class SetPrizeSchemeCommandHandler(
    ILeagueRepository leagueRepository,
    ISeasonRepository seasonRepository,
    ICompetitionRepository competitionRepository,
    IUserManager userManager,
    IDateTimeProvider dateTimeProvider,
    ILogger<SetPrizeSchemeCommandHandler> logger) : IRequestHandler<SetPrizeSchemeCommand>
{
    public async Task Handle(SetPrizeSchemeCommand request, CancellationToken cancellationToken)
    {
        var league = await leagueRepository.GetByIdAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, league, "League");

        var settingUser = await userManager.FindByIdAsync(request.UserId);
        var isSiteAdmin = settingUser != null && await userManager.IsInRoleAsync(settingUser, RoleNames.Administrator);
        var isLeagueAdmin = league.AdministratorUserId == request.UserId;

        var alreadySet = league.PrizeScheme is not null;

        if (alreadySet && !isSiteAdmin)
            throw new BusinessRuleViolationException("The prize scheme has already been set and can only be changed by a site administrator.");

        if (!alreadySet && !isLeagueAdmin && !isSiteAdmin)
            throw new UnauthorizedAccessException("Only the league administrator can set the prize scheme.");

        // A free league with no admin top-up has a £0 pot, so prizes do not apply. The client hides the
        // editor in this case; guard here too so a scheme can never be persisted against an empty pot.
        var hasPrizeFund = league.Price > 0 || (league.PrizeFundOverride ?? 0) > 0;
        if (!hasPrizeFund)
            throw new BusinessRuleViolationException("A prize scheme cannot be set on a free league with no prize fund.");

        var season = await seasonRepository.GetByIdAsync(league.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(league.SeasonId, season, "Season");

        var competition = await competitionRepository.GetByIdAsync(season.CompetitionId, cancellationToken);
        Guard.Against.EntityNotFound(season.CompetitionId, competition, "Competition");

        var scheme = PrizeSchemeFactory.Build(
            request.Scheme,
            PrizeSchemeFactory.ToWholePounds(league.Price),
            request.UserId,
            competition.IsTournament,
            dateTimeProvider);

        if (alreadySet)
            league.OverridePrizeScheme(scheme);
        else
            league.SetPrizeScheme(scheme);

        await leagueRepository.SavePrizeSchemeAsync(league.Id, scheme, cancellationToken);

        logger.LogInformation("League (ID: {LeagueId}) prize scheme set by user (ID: {UserId}){Override}", league.Id, request.UserId, alreadySet ? " as a site-admin override" : string.Empty);
    }
}
