using PredictionLeague.Contracts.Admin.Teams;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Admin.Teams;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class UpdateTeamRequestValidator : BaseTeamRequestValidator<UpdateTeamRequest>;