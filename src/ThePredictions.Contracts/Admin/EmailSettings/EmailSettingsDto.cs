using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.EmailSettings;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record EmailSettingsDto(bool EmailsEnabled);
