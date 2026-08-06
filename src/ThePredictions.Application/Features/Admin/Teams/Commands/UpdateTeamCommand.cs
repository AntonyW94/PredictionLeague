using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.Admin.Teams.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record UpdateTeamCommand(
    int Id,
    string Name,
    string ShortName,
    string LogoUrl,
    string Abbreviation,
    int? ApiTeamId) : IRequest;
