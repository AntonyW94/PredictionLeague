using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.Admin.Seasons.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record UpdateSeasonStatusCommand(int SeasonId, bool IsActive) : IRequest;
