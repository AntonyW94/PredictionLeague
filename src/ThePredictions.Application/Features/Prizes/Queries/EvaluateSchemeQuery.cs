using MediatR;
using ThePredictions.Contracts.Prizes;

namespace ThePredictions.Application.Features.Prizes.Queries;

/// <summary>
/// Evaluates a draft prize scheme for the create/edit editor's live preview - showing the derived
/// prize amounts at a hypothetical entrant count before the scheme is saved.
/// </summary>
public record EvaluateSchemeQuery(
    int SeasonId,
    decimal Price,
    int EntrantCount,
    PrizeSchemeRequest Scheme
) : IRequest<PrizeBreakdownDto>;
