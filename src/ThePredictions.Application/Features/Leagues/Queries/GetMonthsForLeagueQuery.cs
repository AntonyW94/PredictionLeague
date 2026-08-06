using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Leagues.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetMonthsForLeagueQuery(int LeagueId, string CurrentUserId) : IRequest<IEnumerable<MonthDto>>;
