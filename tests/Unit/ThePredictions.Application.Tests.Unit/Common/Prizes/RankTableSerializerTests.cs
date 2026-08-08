using FluentAssertions;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Domain.Services.Prizes;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Prizes;

public class RankTableSerializerTests
{
    [Fact]
    public void SerializeThenDeserialize_ShouldRoundTrip()
    {
        var table = new RankTable(new[]
        {
            new RankBand(2, 5, new[] { 100 }),
            new RankBand(6, null, new[] { 70, 30 })
        });

        var json = RankTableSerializer.Serialize(table);
        var result = RankTableSerializer.Deserialize(json);

        result.Bands.Should().HaveCount(2);
        result.PercentagesFor(8).Should().Equal(70, 30);
    }

    [Fact]
    public void Deserialize_ShouldThrow_WhenJsonEmpty()
    {
        var act = () => RankTableSerializer.Deserialize("  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deserialize_ShouldThrow_WhenJsonMalformed()
    {
        var act = () => RankTableSerializer.Deserialize("{ not json");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deserialize_ShouldThrow_WhenEmptyArray()
    {
        var act = () => RankTableSerializer.Deserialize("[]");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deserialize_ShouldThrow_WhenBandInvalid()
    {
        // Percentages do not sum to 100 -> RankBand validation rejects it.
        var act = () => RankTableSerializer.Deserialize("[{\"MinEntrants\":2,\"MaxEntrants\":5,\"Percentages\":[60,30]}]");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deserialize_ShouldThrow_WhenTheJsonIsTheLiteralNull()
    {
        // "null" parses cleanly but yields no bands, so it has to be rejected like an empty array
        // rather than producing a table with nothing in it.
        var act = () => RankTableSerializer.Deserialize("null");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deserialize_ShouldTreatAMissingPercentagesListAsEmpty()
    {
        // A band with no percentages is invalid, but it must fail the band's own validation rather
        // than throwing a null reference while reading the payload.
        var act = () => RankTableSerializer.Deserialize("[{\"MinEntrants\":2,\"MaxEntrants\":5}]");

        act.Should().Throw<ArgumentException>();
    }
}
