using MediatR;

namespace ThePredictions.Application.Features.External.Tasks.Commands;

public record FreezeDuePrizeSchemesCommand : IRequest<FreezeDuePrizeSchemesResult>;
