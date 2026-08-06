using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public class ProcessPrizesCommand : IRequest<Unit>
{
    public int RoundId { get; init; }
    public int LeagueId { get; init; }
}
