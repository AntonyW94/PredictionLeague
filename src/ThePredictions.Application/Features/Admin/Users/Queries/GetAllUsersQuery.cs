using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Admin.Users;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetAllUsersQuery : IRequest<IEnumerable<UserDto>>;
