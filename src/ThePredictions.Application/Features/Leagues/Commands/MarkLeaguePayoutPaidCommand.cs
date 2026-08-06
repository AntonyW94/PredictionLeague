using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.Leagues.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record MarkLeaguePayoutPaidCommand(int LeagueId, string WinnerUserId, string RequestingUserId) : IRequest;
