using MediatR;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.RunningCosts.Commands;

public record CreateRunningCostCommand(
    string Name,
    decimal Amount,
    CostFrequency Frequency,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    CostPayer Payer,
    string? Notes) : IRequest;
