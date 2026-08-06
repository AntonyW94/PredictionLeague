using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Users;

[ExcludeFromCodeCoverage]
public class DeleteUserRequest
{
    public string? NewAdministratorId { get; init; }
}
