using MediatR;
using PredictionLeague.Application.Common.Interfaces;

namespace PredictionLeague.Application.Features.Admin.Users.Commands;

public record DeleteUserCommand(
    string UserIdToDelete,
    string DeletingUserId,
    bool IsDeletingUserAdmin,
    string? NewAdministratorId
) : IRequest, ITransactionalRequest;