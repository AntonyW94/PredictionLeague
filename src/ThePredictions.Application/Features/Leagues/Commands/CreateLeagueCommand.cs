using MediatR;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Contracts.Prizes;

namespace ThePredictions.Application.Features.Leagues.Commands;

public record CreateLeagueCommand(
    string Name,
    int SeasonId,
    decimal Price,
    string CreatingUserId,
    DateTime EntryDeadlineUtc,
    int PointsForExactScore,
    int PointsForCorrectResult,
    string? BankAccountName = null,
    string? BankSortCode = null,
    string? BankAccountNumber = null,
    string? PaymentReferenceTemplate = null,
    PrizeSchemeRequest? PrizeScheme = null,
    decimal? PrizeFundOverride = null
) : IRequest<LeagueDto>, ITransactionalRequest;