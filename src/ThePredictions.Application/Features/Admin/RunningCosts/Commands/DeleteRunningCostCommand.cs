using MediatR;

namespace ThePredictions.Application.Features.Admin.RunningCosts.Commands;

public record DeleteRunningCostCommand(int Id) : IRequest;
