using MediatR;
using PredictionLeague.Application.Common.Interfaces;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public record SendScheduledRemindersCommand : IRequest, ITransactionalRequest;