namespace ThePredictions.SchemaCheck;

/// <summary>
/// A column as SQL Server describes it, with the CLR type Dapper's reader will hand back.
/// </summary>
public sealed record ResultColumn(int Position, string Name, string SqlType, string ClrType);
