using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Teams;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class CreateTeamRequest : BaseTeamRequest;
