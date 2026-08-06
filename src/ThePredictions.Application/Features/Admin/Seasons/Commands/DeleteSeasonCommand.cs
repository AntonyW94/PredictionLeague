using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Common.Interfaces;

namespace ThePredictions.Application.Features.Admin.Seasons.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record DeleteSeasonCommand(int SeasonId) : IRequest, ITransactionalRequest;
