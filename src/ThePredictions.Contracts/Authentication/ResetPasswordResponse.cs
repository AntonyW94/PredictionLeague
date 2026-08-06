using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Authentication;

[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
[ExcludeFromCodeCoverage]
public abstract record ResetPasswordResponse(bool IsSuccess);
