using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Dashboard;

namespace ThePredictions.Application.Features.Dashboard.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetPendingRequestsQuery(string UserId) : IRequest<IEnumerable<LeagueRequestDto>>;
