using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Dashboard.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetMyLeaguesQuery(string UserId) : IRequest<IEnumerable<MyLeagueDto>>;
