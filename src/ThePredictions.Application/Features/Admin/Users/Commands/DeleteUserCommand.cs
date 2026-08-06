using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Common.Interfaces;

namespace ThePredictions.Application.Features.Admin.Users.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record DeleteUserCommand(
    string UserIdToDelete,
    string DeletingUserId,
    string? NewAdministratorId
) : IRequest, ITransactionalRequest;
