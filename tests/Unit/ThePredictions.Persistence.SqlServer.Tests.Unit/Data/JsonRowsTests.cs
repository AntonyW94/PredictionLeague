using System.Text.Json;
using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Persistence.SqlServer.Data;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Unit.Data;

/// <summary>
/// The JSON the set-based writes send, and the parameter it travels as.
///
/// Everything the <c>OPENJSON ... WITH</c> clauses on the other side depend on is decided here: the property
/// names they match on, the forms each value takes, and the parameter's declared type. None of it is visible
/// to the compiler, so it is pinned rather than assumed.
/// </summary>
public class JsonRowsTests
{
    [Fact]
    public void From_ShouldNameEachPropertyAsTheWithClauseExpects()
    {
        // The WITH clauses match on 'strict $.PascalCase', so a serialiser configured to camel-case names
        // would make every path in the adapter miss - loudly, thanks to strict, but everywhere at once.
        var parameter = JsonRows.From(new[] { new { LeagueId = 7, UserId = "abc" } });

        parameter.Value.Should().Be("""[{"LeagueId":7,"UserId":"abc"}]""");
    }

    [Fact]
    public void From_ShouldWriteEveryRow()
    {
        var parameter = JsonRows.From(new[] { new { Id = 1 }, new { Id = 2 }, new { Id = 3 } });

        parameter.Value.Should().Be("""[{"Id":1},{"Id":2},{"Id":3}]""");
    }

    [Fact]
    public void From_ShouldWriteANullAsAPresentProperty()
    {
        // strict mode fails on a property that is absent, not on one that is null - so a null value has to
        // arrive as "AppliedBoostCode":null rather than being dropped from the object.
        var parameter = JsonRows.From(new[] { new { AppliedBoostCode = (string?)null } });

        parameter.Value.Should().Be("""[{"AppliedBoostCode":null}]""");
    }

    [Fact]
    public void From_ShouldWriteADateTimeInAFormSqlServerReads()
    {
        // Round-tripped through OPENJSON at full datetime2(7) precision, verified against the live server.
        var parameter = JsonRows.From(new[]
        {
            new { SentAtUtc = new DateTime(2026, 8, 24, 21, 6, 18, DateTimeKind.Utc).AddTicks(1234567) }
        });

        parameter.Value.Should().Be("""[{"SentAtUtc":"2026-08-24T21:06:18.1234567Z"}]""");
    }

    [Fact]
    public void From_ShouldWriteADecimalUnquoted()
    {
        var parameter = JsonRows.From(new[] { new { Amount = 9.99m } });

        parameter.Value.Should().Be("""[{"Amount":9.99}]""");
    }

    [Fact]
    public void From_ShouldWriteABooleanAsJsonTrueOrFalse()
    {
        // The bit columns are read straight from these, so 1/0 or "True" would both be wrong.
        var parameter = JsonRows.From(new[] { new { HasBoost = true }, new { HasBoost = false } });

        parameter.Value.Should().Be("""[{"HasBoost":true},{"HasBoost":false}]""");
    }

    [Fact]
    public void From_ShouldWriteAnEnumAsItsNumber_WhenItIsPassedAsOne()
    {
        // Which is why the repositories cast an enum bound for an int column rather than passing it whole:
        // the serialiser's own default for an enum is not something the SQL should depend on.
        var parameter = JsonRows.From(new[] { new { Outcome = (int)PredictionOutcome.ExactScore } });

        parameter.Value.Should().Be("[{\"Outcome\":" + (int)PredictionOutcome.ExactScore + "}]");
    }

    [Fact]
    public void From_ShouldEscapeTextRatherThanBreakTheJson()
    {
        // A player's name is user input arriving in a statement's parameter. It cannot inject SQL - this is
        // still one parameter - but unescaped it could break the JSON, so the round trip is what matters.
        const string awkward = "Zoë O'Neill <admin> \"quoted\" 🎉";

        var parameter = JsonRows.From(new[] { new { Name = awkward } });

        // Asserted by reading it back rather than by matching the escaped text, because how the encoder spells
        // a character is its business - non-ASCII and markup characters come out as \uXXXX escapes, which
        // OPENJSON decodes. Verified against the live server, surrogate pairs included.
        JsonSerializer.Deserialize<JsonElement>(parameter.Value!)[0]
            .GetProperty("Name").GetString().Should().Be(awkward);
    }

    [Fact]
    public void From_ShouldWriteAnEmptyArray_WhenThereAreNoRows()
    {
        // The repositories return before sending an empty batch, so this never reaches the server - but the
        // serialiser producing "[]" rather than "null" is what makes that a choice rather than a necessity.
        var parameter = JsonRows.From(Array.Empty<object>());

        parameter.Value.Should().Be("[]");
    }

    [Fact]
    public void From_ShouldDeclareTheParameterAsUnicodeMax()
    {
        // Pinned rather than sized from the content: Dapper would send nvarchar(4000) for a small batch and
        // nvarchar(max) for a large one, which is two parameter signatures and two cached plans for one
        // statement. ADR-0015 exists because a recompile on this instance cost ~400ms.
        var parameter = JsonRows.From(new[] { new { Id = 1 } });

        parameter.IsAnsi.Should().BeFalse();
        parameter.IsFixedLength.Should().BeFalse();
        parameter.Length.Should().Be(-1);
    }
}
