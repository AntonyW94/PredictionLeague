namespace ThePredictions.SchemaCheck;

/// <summary>
/// A column as SQL Server describes it, with the CLR type Dapper's reader will hand back.
/// </summary>
/// <param name="IsNullable">
/// Whether the column can come back null.
/// </param>
/// <param name="FromTableColumn">
/// Whether the column is a plain reference to a table column, rather than an expression. Only then is
/// <paramref name="IsNullable"/> the table's own answer and worth acting on: <c>sp_describe_first_result_set</c> is
/// conservative about expressions, marking a <c>CASE</c>, an aggregate or an outer-joined column nullable whether or not
/// the statement can actually produce a null. Checking those would bury a real finding in guesses.
/// </param>
public sealed record ResultColumn(
    int Position,
    string Name,
    string SqlType,
    string ClrType,
    bool IsNullable,
    bool FromTableColumn);
