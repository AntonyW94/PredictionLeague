using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Competitions;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class UpdateCompetitionRequest : BaseCompetitionRequest;
