using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Matches;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class CreateMatchRequest : BaseMatchRequest;
