using MediatR;
using ThePredictions.Application.Common.Interfaces;

namespace ThePredictions.Application.Features.Leagues.Commands;

public record SetLeagueArchivedCommand(int LeagueId, string UserId, bool IsArchived) : IRequest, ITransactionalRequest;
