using PredictionLeague.Contracts.Admin.Matches;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Admin.Matches;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class CreateMatchRequestValidator : BaseMatchRequestValidator<CreateMatchRequest>;