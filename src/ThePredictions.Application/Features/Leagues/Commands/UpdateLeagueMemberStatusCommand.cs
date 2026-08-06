using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record UpdateLeagueMemberStatusCommand(
    int LeagueId,
    string MemberId,
    string UpdatingUserId,
    LeagueMemberStatus NewStatus
) : IRequest;
