using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.EmailSettings;

[ExcludeFromCodeCoverage]
public record EmailSettingsDto(bool EmailsEnabled);
