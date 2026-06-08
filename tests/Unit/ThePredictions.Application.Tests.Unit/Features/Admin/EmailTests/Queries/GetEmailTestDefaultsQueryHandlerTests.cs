using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.EmailTests.Queries;
using ThePredictions.Application.Services;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.EmailTests.Queries;

public class GetEmailTestDefaultsQueryHandlerTests
{
    private readonly IEmailTemplateCatalog _catalog = Substitute.For<IEmailTemplateCatalog>();
    private readonly IApplicationReadDbConnection _readDb = Substitute.For<IApplicationReadDbConnection>();
    private readonly IEmailTestDefaultsResolver _resolver = Substitute.For<IEmailTestDefaultsResolver>();
    private readonly GetEmailTestDefaultsQueryHandler _handler;

    public GetEmailTestDefaultsQueryHandlerTests()
    {
        var siteSettings = Options.Create(new SiteSettings { BaseUrl = "https://test.local" });
        _handler = new GetEmailTestDefaultsQueryHandler(_catalog, _readDb, _resolver, siteSettings);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyDefaults_WhenTemplateNotFound()
    {
        _catalog.GetTemplatesAsync(CancellationToken.None).Returns(new List<EmailTemplateInfo>());

        var result = await _handler.Handle(new GetEmailTestDefaultsQuery(99, "user-1"), CancellationToken.None);

        result.Defaults.Should().BeEmpty();
        _resolver.DidNotReceiveWithAnyArgs().Resolve(default!, default!, default!);
    }

    [Fact]
    public async Task Handle_ShouldResolveDefaults_WhenTemplateAndUserFound()
    {
        var template = new EmailTemplateInfo(5, "League Join Approved", "You're in", true, ["FIRST_NAME"]);
        _catalog.GetTemplatesAsync(CancellationToken.None).Returns(new List<EmailTemplateInfo> { template });

        var user = new EmailTestUserData("Antony", "Willson", "antony@example.com");
        _readDb.QuerySingleOrDefaultAsync<EmailTestUserData>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(user);

        _resolver.Resolve(Arg.Any<IReadOnlyList<string>>(), Arg.Any<EmailTestUserData>(), "https://test.local")
            .Returns(new Dictionary<string, string> { ["FIRST_NAME"] = "Antony" });

        var result = await _handler.Handle(new GetEmailTestDefaultsQuery(5, "user-1"), CancellationToken.None);

        result.Defaults.Should().ContainKey("FIRST_NAME").WhoseValue.Should().Be("Antony");
        _resolver.Received(1).Resolve(Arg.Is<IReadOnlyList<string>>(p => p.Contains("FIRST_NAME")), user, "https://test.local");
    }

    [Fact]
    public async Task Handle_ShouldUseEmptyUser_WhenDataUserNotFound()
    {
        var template = new EmailTemplateInfo(5, "League Join Approved", "You're in", true, ["FIRST_NAME"]);
        _catalog.GetTemplatesAsync(CancellationToken.None).Returns(new List<EmailTemplateInfo> { template });

        _readDb.QuerySingleOrDefaultAsync<EmailTestUserData>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns((EmailTestUserData?)null);

        _resolver.Resolve(Arg.Any<IReadOnlyList<string>>(), Arg.Any<EmailTestUserData>(), Arg.Any<string>())
            .Returns(new Dictionary<string, string>());

        await _handler.Handle(new GetEmailTestDefaultsQuery(5, "missing"), CancellationToken.None);

        _resolver.Received(1).Resolve(
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Is<EmailTestUserData>(u => u.FirstName == string.Empty && u.Email == string.Empty),
            Arg.Any<string>());
    }
}
