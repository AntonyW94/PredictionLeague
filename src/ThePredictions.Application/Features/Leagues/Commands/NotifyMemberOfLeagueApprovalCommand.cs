using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.Leagues.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record NotifyMemberOfLeagueApprovalCommand(
    string MemberUserId,
    int LeagueId,
    string LeagueName,
    int SeasonId) : IRequest;
