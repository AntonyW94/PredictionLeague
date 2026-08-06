using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Admin.Rounds;

namespace ThePredictions.Application.Features.Dashboard.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetMatchesForRoundQuery(int RoundId) : IRequest<IEnumerable<MatchInRoundDto>>;
