using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Boosts.Queries;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services.Boosts;
using ThePredictions.Contracts.Boosts;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Boosts.Queries;

/// <summary>
/// The boost picker on the predictions page. Despite its old "SQL plus a mapping" label this handler
/// runs no SQL at all - it reads the league's boost definitions and then asks the boost service, once
/// per boost, whether this player may use it in this round. That fan-out is the thing worth pinning
/// down: eligibility is per boost, so asking once and reusing the answer would offer a player a boost
/// they have already spent.
/// </summary>
public class GetAvailableBoostsQueryHandlerTests
{
    private const int LeagueId = 10;
    private const int RoundId = 5;
    private const string UserId = "user-1";

    private readonly IBoostReadRepository _boostReadRepository = Substitute.For<IBoostReadRepository>();
    private readonly IBoostService _boostService = Substitute.For<IBoostService>();
    private readonly GetAvailableBoostsQueryHandler _handler;

    public GetAvailableBoostsQueryHandlerTests()
    {
        _handler = new GetAvailableBoostsQueryHandler(_boostReadRepository, _boostService);
    }

    private static BoostDefinition Definition(
        string code = "DOUBLE_UP",
        string? tooltip = "Doubles your points",
        string? description = "Use once per half",
        string? imageUrl = "https://example.test/b.png",
        string? selectedImageUrl = "https://example.test/b-on.png",
        string? disabledImageUrl = "https://example.test/b-off.png") =>
        new(code, $"Name {code}", tooltip, description, imageUrl, selectedImageUrl, disabledImageUrl);

    private void GivenDefinitions(params BoostDefinition[] definitions) =>
        _boostReadRepository.GetBoostDefinitionsForLeagueAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(definitions);

    private void GivenEligibility(string boostCode, bool canUse) =>
        _boostService.GetEligibilityAsync(UserId, LeagueId, RoundId, boostCode, Arg.Any<CancellationToken>())
            .Returns(new BoostEligibilityDto { BoostCode = boostCode, LeagueId = LeagueId, RoundId = RoundId, CanUse = canUse });

    private Task<List<BoostOptionDto>> HandleAsync() =>
        _handler.Handle(new GetAvailableBoostsQuery(LeagueId, RoundId, UserId), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenTheLeagueHasNoBoosts()
    {
        GivenDefinitions();

        (await HandleAsync()).Should().BeEmpty();
    }

    // The early return matters: without it the handler would still call Task.WhenAll on an empty set
    // and, more to the point, a league with boosts switched off must not reach the boost service.
    [Fact]
    public async Task Handle_ShouldNotAskAboutEligibility_WhenTheLeagueHasNoBoosts()
    {
        GivenDefinitions();

        await HandleAsync();

        await _boostService.DidNotReceiveWithAnyArgs()
            .GetEligibilityAsync(default!, default, default, default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldAskAboutEachBoostSeparately()
    {
        GivenDefinitions(Definition("DOUBLE_UP"), Definition("WILDCARD"));
        GivenEligibility("DOUBLE_UP", canUse: true);
        GivenEligibility("WILDCARD", canUse: false);

        await HandleAsync();

        await _boostService.Received(1).GetEligibilityAsync(UserId, LeagueId, RoundId, "DOUBLE_UP", Arg.Any<CancellationToken>());
        await _boostService.Received(1).GetEligibilityAsync(UserId, LeagueId, RoundId, "WILDCARD", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAttachEachBoostsOwnEligibility()
    {
        GivenDefinitions(Definition("DOUBLE_UP"), Definition("WILDCARD"));
        GivenEligibility("DOUBLE_UP", canUse: true);
        GivenEligibility("WILDCARD", canUse: false);

        var result = await HandleAsync();

        result.Single(b => b.BoostCode == "DOUBLE_UP").Eligibility!.CanUse.Should().BeTrue();
        result.Single(b => b.BoostCode == "WILDCARD").Eligibility!.CanUse.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnOneOptionPerBoost()
    {
        GivenDefinitions(Definition("DOUBLE_UP"), Definition("WILDCARD"), Definition("TRIPLE"));
        foreach (var code in new[] { "DOUBLE_UP", "WILDCARD", "TRIPLE" })
            GivenEligibility(code, canUse: true);

        (await HandleAsync()).Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_ShouldCarryTheBoostPresentationThrough()
    {
        GivenDefinitions(Definition());
        GivenEligibility("DOUBLE_UP", canUse: true);

        var option = (await HandleAsync()).Single();

        option.Name.Should().Be("Name DOUBLE_UP");
        option.Tooltip.Should().Be("Doubles your points");
        option.Description.Should().Be("Use once per half");
        option.ImageUrl.Should().Be("https://example.test/b.png");
        option.SelectedImageUrl.Should().Be("https://example.test/b-on.png");
        option.DisabledImageUrl.Should().Be("https://example.test/b-off.png");
    }

    // The definition allows these to be absent, but the page binds them straight into markup, so they
    // become empty strings rather than nulls.
    [Fact]
    public async Task Handle_ShouldSubstituteEmptyStrings_WhenTheBoostHasNoTextOrImages()
    {
        GivenDefinitions(Definition(tooltip: null, description: null, imageUrl: null, selectedImageUrl: null, disabledImageUrl: null));
        GivenEligibility("DOUBLE_UP", canUse: true);

        var option = (await HandleAsync()).Single();

        option.Tooltip.Should().BeEmpty();
        option.Description.Should().BeEmpty();
        option.ImageUrl.Should().BeEmpty();
        option.SelectedImageUrl.Should().BeEmpty();
        option.DisabledImageUrl.Should().BeEmpty();
    }
}
