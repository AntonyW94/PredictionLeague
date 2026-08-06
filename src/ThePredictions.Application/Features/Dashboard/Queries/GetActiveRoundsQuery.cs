using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Dashboard;

namespace ThePredictions.Application.Features.Dashboard.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetActiveRoundsQuery(string UserId) : IRequest<IEnumerable<ActiveRoundDto>>;
