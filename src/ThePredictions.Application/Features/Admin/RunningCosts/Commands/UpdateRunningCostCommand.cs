using MediatR;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.RunningCosts.Commands;

public record UpdateRunningCostCommand(
    int Id,
    string Name,
    decimal Amount,
    CostFrequency Frequency,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    string? Notes) : IRequest;
