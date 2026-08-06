using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Homepage;

namespace ThePredictions.Application.Features.Homepage.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetHomepageSeasonsQuery : IRequest<IEnumerable<HomepageSeasonDto>>;
