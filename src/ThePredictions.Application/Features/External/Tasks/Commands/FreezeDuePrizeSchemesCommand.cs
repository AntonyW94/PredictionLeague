using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.External.Tasks.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record FreezeDuePrizeSchemesCommand : IRequest<FreezeDuePrizeSchemesResult>;
