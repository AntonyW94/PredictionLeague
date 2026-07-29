namespace ThePredictions.SchemaCheck;

/// <summary>
/// A SQL parameter and the type it will be declared as for sp_describe_first_result_set.
/// <see cref="WasGuessed"/> marks the ones inferred from the parameter's name rather than read off the
/// anonymous object at the call site: a guessed type can change the described type of a CASE expression
/// that returns the parameter, so any mismatch involving guesses is re-checked under the alternative
/// typing before it is reported.
/// </summary>
public sealed record InferredParameter(string Name, string SqlType, bool WasGuessed);
