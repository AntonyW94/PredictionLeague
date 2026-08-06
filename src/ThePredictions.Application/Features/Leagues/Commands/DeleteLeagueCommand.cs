using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Common.Interfaces;

namespace ThePredictions.Application.Features.Leagues.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record DeleteLeagueCommand(
    int LeagueId,
    string DeletingUserId,
    bool IsAdmin) : IRequest, ITransactionalRequest;
