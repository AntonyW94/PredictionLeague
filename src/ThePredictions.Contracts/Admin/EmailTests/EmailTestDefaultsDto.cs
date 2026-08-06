using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.EmailTests;

[ExcludeFromCodeCoverage]
public record EmailTestDefaultsDto(Dictionary<string, string> Defaults);
