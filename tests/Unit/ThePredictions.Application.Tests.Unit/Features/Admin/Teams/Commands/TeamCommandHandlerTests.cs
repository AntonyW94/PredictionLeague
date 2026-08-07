using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Teams.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Teams.Commands;

/// <summary>
/// Managing the teams fixtures are played between.
/// </summary>
public class TeamCommandHandlerTests
{
    private const int TeamId = 101;

    private readonly ITeamRepository _repository = Substitute.For<ITeamRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private readonly CreateTeamCommandHandler _create;
    private readonly UpdateTeamCommandHandler _update;

    public TeamCommandHandlerTests()
    {
        _create = new CreateTeamCommandHandler(_repository, _currentUser);
        _update = new UpdateTeamCommandHandler(_repository, _currentUser);

        _repository.CreateAsync(Arg.Any<Team>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var source = call.Arg<Team>();
            return new Team(id: TeamId, name: source.Name, shortName: source.ShortName,
                logoUrl: source.LogoUrl, abbreviation: source.Abbreviation, apiTeamId: source.ApiTeamId);
        });
    }

    private Team GivenExisting()
    {
        var team = new Team(id: TeamId, name: "Arsenal", shortName: "Arsenal",
            logoUrl: "arsenal.png", abbreviation: "ARS", apiTeamId: 42);
        _repository.GetByIdAsync(TeamId, Arg.Any<CancellationToken>()).Returns(team);
        return team;
    }

    private Task<Contracts.Admin.Teams.TeamDto> CreateAsync() =>
        _create.Handle(new CreateTeamCommand("Arsenal", "Arsenal", "arsenal.png", "ARS", 42), CancellationToken.None);

    private Task UpdateAsync() =>
        _update.Handle(new UpdateTeamCommand(TeamId, "Arsenal FC", "Arsenal", "new.png", "AFC", 43), CancellationToken.None);

    [Fact]
    public async Task Create_ShouldRequireAnAdministrator()
    {
        await CreateAsync();

        _currentUser.Received(1).EnsureAdministrator();
    }

    [Fact]
    public async Task Create_ShouldSaveTheTeamAndReportItBack()
    {
        var result = await CreateAsync();

        await _repository.Received(1).CreateAsync(
            Arg.Is<Team>(t => t.Name == "Arsenal" && t.Abbreviation == "ARS"), Arg.Any<CancellationToken>());
        result.Id.Should().Be(TeamId);
        result.Name.Should().Be("Arsenal");
        result.ShortName.Should().Be("Arsenal");
        result.LogoUrl.Should().Be("arsenal.png");
        result.Abbreviation.Should().Be("ARS");
        result.ApiTeamId.Should().Be(42);
    }

    [Fact]
    public async Task Update_ShouldRequireAnAdministrator()
    {
        GivenExisting();

        await UpdateAsync();

        _currentUser.Received(1).EnsureAdministrator();
    }

    [Fact]
    public async Task Update_ShouldThrow_WhenTheTeamDoesNotExist()
    {
        var act = () => UpdateAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Update_ShouldApplyTheEditedDetails()
    {
        var existing = GivenExisting();

        await UpdateAsync();

        existing.Name.Should().Be("Arsenal FC");
        existing.LogoUrl.Should().Be("new.png");
        existing.Abbreviation.Should().Be("AFC");
        existing.ApiTeamId.Should().Be(43);
        await _repository.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
    }
}
