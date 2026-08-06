using FluentAssertions;
using NSubstitute;
using ThePredictions.Contracts.SeasonPasses;
using ThePredictions.Web.Client.Services.Dashboard;
using ThePredictions.Web.Client.Services.Leagues;
using ThePredictions.Web.Client.Services.Onboarding;
using ThePredictions.Web.Client.Services.SeasonPasses;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.Services.Dashboard;

/// <summary>
/// The dashboard prompt nudges a player to buy the pass they need before they can join that
/// season's leagues, and has to disappear on its own once they have it.
/// </summary>
public class DashboardStateServicePromptsTests
{
    private readonly ILeagueService _leagueService = Substitute.For<ILeagueService>();
    private readonly ISeasonPassService _seasonPassService = Substitute.For<ISeasonPassService>();
    private readonly IOnboardingService _onboardingService = Substitute.For<IOnboardingService>();

    private async Task<DashboardStateService> BuildWithPassesAsync(params AvailableSeasonPassDto[] passes)
    {
        _seasonPassService.GetAvailablePassesAsync().Returns(passes.ToList());

        var service = new DashboardStateService(_leagueService, _seasonPassService, _onboardingService);
        await service.LoadAvailableSeasonPassesAsync();

        return service;
    }

    private static AvailableSeasonPassDto Pass(int seasonId, string seasonName) =>
        new(seasonId, seasonName, CompetitionLogoUrl: null, RequiresPayment: true,
            StandardPrice: 10m, PremiumPrice: null, IsTrialEligible: false,
            PlayerCount: 0, NextEntryDeadlineUtc: null);

    [Fact]
    public async Task Prompts_ShouldBeEmpty_WhenThereIsNoPassToBuy()
    {
        var service = await BuildWithPassesAsync();

        service.Prompts.Should().BeEmpty();
    }

    [Fact]
    public async Task Prompts_ShouldNudgeThePlayerToBuyAnAvailablePass()
    {
        var service = await BuildWithPassesAsync(Pass(7, "2026/27 Premier League"));

        var prompt = service.Prompts.Should().ContainSingle().Subject;
        prompt.Message.Should().Be("Get your 2026/27 Premier League pass to join its leagues.");
        prompt.ActionLabel.Should().Be("Get pass");
        prompt.ActionHref.Should().Be("/season-passes?seasonId=7");
        prompt.Highlight.Should().Be("2026/27 Premier League");
        prompt.Icon.Should().Be("bi-ticket-perforated-fill");
    }

    [Fact]
    public async Task Prompts_ShouldRaiseOnePromptPerAvailablePass()
    {
        var service = await BuildWithPassesAsync(Pass(1, "Season A"), Pass(2, "Season B"));

        service.Prompts.Should().HaveCount(2);
        service.Prompts.Select(p => p.ActionHref)
            .Should().Equal("/season-passes?seasonId=1", "/season-passes?seasonId=2");
    }

    [Fact]
    public async Task Prompts_ShouldClear_WhenThePassesCanNoLongerBeLoaded()
    {
        // A failed load leaves no passes, so the nudge disappears rather than showing stale advice.
        _seasonPassService.GetAvailablePassesAsync().Returns<List<AvailableSeasonPassDto>>(_ => throw new HttpRequestException("offline"));

        var service = new DashboardStateService(_leagueService, _seasonPassService, _onboardingService);
        await service.LoadAvailableSeasonPassesAsync();

        service.Prompts.Should().BeEmpty();
    }
}
