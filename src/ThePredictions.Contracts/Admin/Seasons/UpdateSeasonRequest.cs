using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Seasons;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class UpdateSeasonRequest : BaseSeasonRequest;
