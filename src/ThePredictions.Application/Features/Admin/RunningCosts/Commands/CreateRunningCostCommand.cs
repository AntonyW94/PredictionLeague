using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.RunningCosts.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record CreateRunningCostCommand(
    string Name,
    decimal Amount,
    CostFrequency Frequency,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    string? Notes) : IRequest;
