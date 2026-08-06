using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Authentication;

[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public abstract record ResetPasswordResponse(bool IsSuccess);
