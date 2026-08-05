using FluentAssertions;
using Microsoft.JSInterop;
using NSubstitute;
using ThePredictions.Web.Client.Services.Time;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.Services.Time;

public class LocalDayBoundaryConverterTests
{
    private const string GetTimezoneOffset = "blazorInterop.getTimezoneOffset";

    /// <summary>
    /// JavaScript reports the offset as UTC minus local, so British Summer Time is -60 and GMT is 0.
    /// UTC is therefore the local time plus that offset.
    /// </summary>
    private const int BritishSummerTime = -60;
    private const int Greenwich = 0;
    private const int IndiaStandardTime = -330;

    private readonly IJSRuntime _jsRuntime = Substitute.For<IJSRuntime>();
    private readonly LocalDayBoundaryConverter _converter;

    public LocalDayBoundaryConverterTests()
    {
        _converter = new LocalDayBoundaryConverter(_jsRuntime);
    }

    [Fact]
    public async Task StartOfDayUtcAsync_ShouldReturnTheInstantTheLocalDayBegins_WhenAheadOfUtc()
    {
        GivenOffset(BritishSummerTime);

        var result = await _converter.StartOfDayUtcAsync(new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Unspecified));

        // Midnight on the 4th in BST is 23:00 on the 3rd in UTC.
        result.Should().Be(new DateTime(2026, 8, 3, 23, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public async Task StartOfNextDayUtcAsync_ShouldReturnTheInstantTheFollowingLocalDayBegins_WhenAheadOfUtc()
    {
        GivenOffset(BritishSummerTime);

        var result = await _converter.StartOfNextDayUtcAsync(new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Unspecified));

        // Exclusive upper bound: a pass bought at 23:59 local on the 4th is still inside it.
        result.Should().Be(new DateTime(2026, 8, 4, 23, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public async Task StartOfDayUtcAsync_ShouldReturnTheSameInstant_WhenOnGreenwichTime()
    {
        GivenOffset(Greenwich);

        var result = await _converter.StartOfDayUtcAsync(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Unspecified));

        result.Should().Be(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public async Task StartOfDayUtcAsync_ShouldHandleAnOffsetThatIsNotAWholeHour_WhenTheZoneNeedsIt()
    {
        GivenOffset(IndiaStandardTime);

        var result = await _converter.StartOfDayUtcAsync(new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Unspecified));

        // Midnight in Kolkata (UTC+5:30) is 18:30 the previous day in UTC.
        result.Should().Be(new DateTime(2026, 8, 3, 18, 30, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public async Task StartOfDayUtcAsync_ShouldDiscardTheTimeOfDay_WhenThePickedValueCarriesOne()
    {
        GivenOffset(Greenwich);

        var result = await _converter.StartOfDayUtcAsync(new DateTime(2026, 8, 4, 17, 42, 33, DateTimeKind.Unspecified));

        result.Should().Be(new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public async Task StartOfDayUtcAsync_ShouldAskForTheOffsetAtThatBoundary_SoAClockChangeIsRespected()
    {
        GivenOffset(BritishSummerTime);

        await _converter.StartOfDayUtcAsync(new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Unspecified));

        // No Z suffix, so JavaScript reads it as local and reports the offset in force on that date
        // rather than the offset in force today.
        await _jsRuntime
            .Received(1)
            .InvokeAsync<int>(GetTimezoneOffset, Arg.Is<object?[]?>(args => (string)args![0]! == "2026-08-04T00:00:00"));
    }

    [Fact]
    public async Task StartOfNextDayUtcAsync_ShouldAskForTheOffsetAtTheFollowingDay_WhenBoundingTheEnd()
    {
        GivenOffset(BritishSummerTime);

        await _converter.StartOfNextDayUtcAsync(new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Unspecified));

        await _jsRuntime
            .Received(1)
            .InvokeAsync<int>(GetTimezoneOffset, Arg.Is<object?[]?>(args => (string)args![0]! == "2026-10-25T00:00:00"));
    }

    [Fact]
    public async Task StartOfDayUtcAsync_ShouldFallBackToTreatingTheDayAsUtc_WhenTheBrowserCannotBeAsked()
    {
        _jsRuntime
            .InvokeAsync<int>(GetTimezoneOffset, Arg.Any<object?[]?>())
            .Returns<ValueTask<int>>(_ => throw new JSException("no interop"));

        var result = await _converter.StartOfDayUtcAsync(new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Unspecified));

        result.Should().Be(new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Unspecified));
    }

    private void GivenOffset(int offsetMinutes)
    {
        _jsRuntime
            .InvokeAsync<int>(GetTimezoneOffset, Arg.Any<object?[]?>())
            .Returns(offsetMinutes);
    }
}
