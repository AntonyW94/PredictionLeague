using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Resolves the entry-fee payment details for a league. Visible to the admin and members; a
/// prospective joiner who supplies the matching <paramref name="EntryCode"/> is also authorised
/// (the code is the league's access credential, and they need the details to pay).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetLeaguePaymentInfoQuery(int LeagueId, string RequestingUserId, string? EntryCode = null) : IRequest<LeaguePaymentInfoDto>;
