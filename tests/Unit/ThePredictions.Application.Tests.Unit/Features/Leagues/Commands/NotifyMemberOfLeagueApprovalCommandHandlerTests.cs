using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Services;
using static ThePredictions.Application.Features.Leagues.Commands.NotifyMemberOfLeagueApprovalCommandHandler;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Commands;

/// <summary>
/// The "you're in - here's your league" email sent once a join request is approved. It stays quiet
/// rather than failing when the template has not been set up in Brevo yet, because the approval
/// itself has already happened and must not be rolled back over an email.
/// </summary>
public class NotifyMemberOfLeagueApprovalCommandHandlerTests
{
    private const long TemplateId = 301;
    private const int LeagueId = 7;
    private const int SeasonId = 11;

    private readonly IApplicationReadDbConnection _dbConnection = Substitute.For<IApplicationReadDbConnection>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();

    private NotifyMemberOfLeagueApprovalCommandHandler CreateHandler(long? templateId = TemplateId, string? baseUrl = "https://test.local")
    {
        var brevo = Options.Create(new BrevoSettings
        {
            Templates = templateId == null ? null : new TemplateSettings { LeagueJoinApproved = templateId.Value }
        });

        return new NotifyMemberOfLeagueApprovalCommandHandler(
            _dbConnection, _emailService, brevo, Options.Create(new SiteSettings { BaseUrl = baseUrl }));
    }

    private void GivenMember(string email = "alice@example.com", string firstName = "Alice", string seasonName = "2026/27") =>
        _dbConnection.QuerySingleOrDefaultAsync<LeagueMemberContactRow>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(new LeagueMemberContactRow(email, firstName, seasonName));

    private Task HandleAsync(long? templateId = TemplateId, string? baseUrl = "https://test.local") =>
        CreateHandler(templateId, baseUrl).Handle(
            new NotifyMemberOfLeagueApprovalCommand("user-1", LeagueId, "The Office League", SeasonId),
            CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenNoTemplatesAreConfigured()
    {
        await HandleAsync(templateId: null);

        await _dbConnection.DidNotReceiveWithAnyArgs()
            .QuerySingleOrDefaultAsync<LeagueMemberContactRow>(default!, CancellationToken.None);
        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenThisParticularTemplateIsNotSetUpYet()
    {
        // Calling the mail provider with template id zero would just be an error, so it is skipped.
        await HandleAsync(templateId: 0);

        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheMemberCannotBeFound()
    {
        await HandleAsync();

        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldEmailTheMember()
    {
        GivenMember();

        await HandleAsync();

        await _emailService.Received(1).SendTemplatedEmailAsync(
            "alice@example.com", TemplateId, Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_ShouldPersonaliseTheEmailAndLinkStraightToTheLeague()
    {
        GivenMember(firstName: "Alice", seasonName: "2026/27");

        await HandleAsync();

        await _emailService.Received(1).SendTemplatedEmailAsync(
            Arg.Any<string>(),
            TemplateId,
            Arg.Is<object>(p =>
                Property(p, "FIRST_NAME") == "Alice"
                && Property(p, "LEAGUE_NAME") == "The Office League"
                && Property(p, "SEASON_NAME") == "2026/27"
                && Property(p, "LEAGUE_URL") == $"https://test.local/leagues/{LeagueId}/dashboard"));
    }

    [Fact]
    public async Task Handle_ShouldLinkToTheCanonicalSite_WhenNoBaseUrlIsConfigured()
    {
        // The link is built from configured settings, never a request header, which an attacker
        // could otherwise use to point the email at their own site.
        GivenMember();

        await HandleAsync(baseUrl: null);

        await _emailService.Received(1).SendTemplatedEmailAsync(
            Arg.Any<string>(),
            TemplateId,
            Arg.Is<object>(p => Property(p, "LEAGUE_URL") == $"{SiteSettings.FallbackBaseUrl}/leagues/{LeagueId}/dashboard"));
    }

    private static string Property(object parameters, string name) =>
        (string)parameters.GetType().GetProperty(name)!.GetValue(parameters)!;
}
