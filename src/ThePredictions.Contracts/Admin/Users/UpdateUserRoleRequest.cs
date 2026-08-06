using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Users;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record UpdateUserRoleRequest(string NewRole);
