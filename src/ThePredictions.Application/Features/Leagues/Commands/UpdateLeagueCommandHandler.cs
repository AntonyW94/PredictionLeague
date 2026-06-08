using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class UpdateLeagueCommandHandler(ILeagueRepository leagueRepository, ISeasonRepository seasonRepository, IFieldEncryptionService fieldEncryptionService, IMediator mediator, IDateTimeProvider dateTimeProvider) : IRequestHandler<UpdateLeagueCommand>
{
    public async Task Handle(UpdateLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await leagueRepository.GetByIdAsync(request.Id, cancellationToken);
        Guard.Against.EntityNotFound(request.Id, league, "League");

        if (league.AdministratorUserId != request.UserId)
            throw new UnauthorizedAccessException("Only the league administrator can update the league.");

        if (league.EntryDeadlineUtc < dateTimeProvider.UtcNow)
            throw new InvalidOperationException("This league cannot be edited because its entry deadline has passed.");
      
        if (league.Price != request.Price && league.Members.Count > 1)
            throw new InvalidOperationException("The entry fee cannot be changed after other players have joined the league.");

        var season = await seasonRepository.GetByIdAsync(league.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(league.SeasonId, season, "Season");
        
        league.UpdateDetails(
            request.Name,
            request.Price,
            request.EntryDeadlineUtc,
            request.PointsForExactScore,
            request.PointsForCorrectResult,
            season,
            dateTimeProvider
        );

        league.SetBankDetails(
            fieldEncryptionService.Encrypt(NullIfBlank(request.BankAccountName)),
            fieldEncryptionService.Encrypt(NullIfBlank(request.BankSortCode)),
            fieldEncryptionService.Encrypt(NullIfBlank(request.BankAccountNumber)),
            NullIfBlank(request.PaymentReferenceTemplate));

        league.SetPrizeFundOverride(request.PrizeFundOverride);
        league.SetIsListed(request.IsListed);

        // Toggling approval off auto-approves anyone currently waiting; capture them so we can let them
        // know they can now take part.
        var autoApprovedUserIds = league.SetRequiresMemberApproval(request.RequiresMemberApproval, dateTimeProvider);

        await leagueRepository.UpdateAsync(league, cancellationToken);

        foreach (var memberUserId in autoApprovedUserIds)
        {
            await mediator.Send(new NotifyMemberOfLeagueApprovalCommand(memberUserId, league.Name, league.SeasonId), cancellationToken);
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}