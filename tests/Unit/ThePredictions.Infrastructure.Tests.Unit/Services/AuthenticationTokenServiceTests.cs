using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;
using ThePredictions.Infrastructure.Services;
using Xunit;

namespace ThePredictions.Infrastructure.Tests.Unit.Services;

/// <summary>
/// The access token carries the identity and roles every authorised endpoint trusts, so a missing
/// claim silently breaks permissions and a wrong expiry either logs people out early or keeps a
/// revoked session alive.
/// </summary>
public class AuthenticationTokenServiceTests
{
    private const string UserId = "user-1";
    private static readonly DateTime NowUtc = new(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);

    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly IConfiguration _configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = "a-development-signing-secret-that-is-long-enough-for-hmac-sha256",
            ["JwtSettings:Issuer"] = "https://api.thepredictions.co.uk",
            ["JwtSettings:Audience"] = "https://www.thepredictions.co.uk",
            ["JwtSettings:ExpiryMinutes"] = "15",
            ["JwtSettings:RefreshTokenExpiryDays"] = "30"
        })
        .Build();

    private static ApplicationUser User() => new()
    {
        Id = UserId,
        Email = "player@example.com",
        FirstName = "Alex",
        LastName = "Player"
    };

    private AuthenticationTokenService BuildService(params string[] roles)
    {
        _dateTimeProvider.UtcNow.Returns(NowUtc);
        _userManager.GetRolesAsync(Arg.Any<ApplicationUser>()).Returns(roles.ToList());

        return new AuthenticationTokenService(_userManager, _configuration, _refreshTokenRepository, _dateTimeProvider);
    }

    private static JwtSecurityToken Read(string accessToken) => new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

    [Fact]
    public async Task GenerateTokensAsync_ShouldCarryTheUsersIdentityClaims()
    {
        var result = await BuildService().GenerateTokensAsync(User(), CancellationToken.None);

        var token = Read(result.AccessToken);
        token.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == UserId);
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "player@example.com");
        token.Claims.Should().Contain(c => c.Type == "FirstName" && c.Value == "Alex");
        token.Claims.Should().Contain(c => c.Type == "LastName" && c.Value == "Player");
        token.Claims.Should().Contain(c => c.Type == "FullName" && c.Value == "Alex Player");
    }

    [Fact]
    public async Task GenerateTokensAsync_ShouldIncludeEveryRole()
    {
        var result = await BuildService("Admin", "Moderator").GenerateTokensAsync(User(), CancellationToken.None);

        Read(result.AccessToken).Claims
            .Where(c => c.Type == "role").Select(c => c.Value)
            .Should().BeEquivalentTo("Admin", "Moderator");
    }

    [Fact]
    public async Task GenerateTokensAsync_ShouldIncludeNoRoleClaims_ForAnOrdinaryPlayer()
    {
        var result = await BuildService().GenerateTokensAsync(User(), CancellationToken.None);

        Read(result.AccessToken).Claims.Should().NotContain(c => c.Type == "role");
    }

    [Fact]
    public async Task GenerateTokensAsync_ShouldSetTheIssuerAndAudienceFromConfiguration()
    {
        var result = await BuildService().GenerateTokensAsync(User(), CancellationToken.None);

        var token = Read(result.AccessToken);
        token.Issuer.Should().Be("https://api.thepredictions.co.uk");
        token.Audiences.Should().ContainSingle().Which.Should().Be("https://www.thepredictions.co.uk");
    }

    [Fact]
    public async Task GenerateTokensAsync_ShouldExpireTheAccessTokenAfterTheConfiguredMinutes()
    {
        var result = await BuildService().GenerateTokensAsync(User(), CancellationToken.None);

        result.ExpiresAtUtc.Should().Be(NowUtc.AddMinutes(15));
    }

    [Fact]
    public async Task GenerateTokensAsync_ShouldGiveEachTokenAUniqueIdentifier()
    {
        var service = BuildService();

        var first = await service.GenerateTokensAsync(User(), CancellationToken.None);
        var second = await service.GenerateTokensAsync(User(), CancellationToken.None);

        var firstJti = Read(first.AccessToken).Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var secondJti = Read(second.AccessToken).Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        firstJti.Should().NotBe(secondJti);
    }

    [Fact]
    public async Task GenerateTokensAsync_ShouldStoreARefreshTokenForTheUser()
    {
        var result = await BuildService().GenerateTokensAsync(User(), CancellationToken.None);

        await _refreshTokenRepository.Received(1).CreateAsync(
            Arg.Is<RefreshToken>(t =>
                t.UserId == UserId
                && t.Token == result.RefreshToken
                && t.Created == NowUtc
                && t.Expires == NowUtc.AddDays(30)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateTokensAsync_ShouldIssueADifferentRefreshTokenEachTime()
    {
        var service = BuildService();

        var first = await service.GenerateTokensAsync(User(), CancellationToken.None);
        var second = await service.GenerateTokensAsync(User(), CancellationToken.None);

        first.RefreshToken.Should().NotBe(second.RefreshToken);
        first.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }
}
