using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Prizes;

namespace ThePredictions.Application.Features.Prizes.Queries;

/// <summary>
/// Evaluates a draft prize scheme for the create/edit editor's live preview - showing the derived
/// prize amounts at a hypothetical entrant count before the scheme is saved.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record EvaluateSchemeQuery(
    int SeasonId,
    decimal Price,
    int EntrantCount,
    PrizeSchemeRequest Scheme,
    decimal? PrizeFundOverride = null
) : IRequest<PrizeBreakdownDto>;
