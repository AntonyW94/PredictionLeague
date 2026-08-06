using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Prizes;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The prospective-member prize preview resolved from a private league's entry code, shown as a
/// confirm step before the join request is sent. Holding the code is the authorisation.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetPrizePreviewByCodeQuery(string EntryCode, string RequestingUserId) : IRequest<PrizePreviewDto>;
