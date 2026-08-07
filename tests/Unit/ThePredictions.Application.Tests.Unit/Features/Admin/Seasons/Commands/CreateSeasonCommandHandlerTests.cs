using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ThePredictions.Application.Features.Admin.Seasons.Commands;
using ThePredictions.Application.Features.Admin.Teams.Queries;
using ThePredictions.Application.FootballApi.DTOs;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Contracts.Admin.Teams;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Commands;

/// <summary>
/// Creating a season is the one chance to catch a mismatch with the football data feed. Get the
/// dates, round count or teams wrong and every fixture sync afterwards is against the wrong season,
/// so the handler refuses rather than creating something subtly broken.
/// </summary>
public class CreateSeasonCommandHandlerTests
{
    private const int CompetitionId = 3;
    private const int ApiLeagueId = 39;
    private const int CreatedSeasonId = 11;

    private static readonly DateTime FixedNow = new(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonStart = new(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonEnd = new(2027, 5, 23, 0, 0, 0, DateTimeKind.Utc);

    private readonly ISeasonRepository _seasonRepository = Substitute.For<ISeasonRepository>();
    private readonly ICompetitionRepository _competitionRepository = Substitute.For<ICompetitionRepository>();
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly IRoundRepository _roundRepository = Substitute.For<IRoundRepository>();
    private readonly ITournamentRoundMappingRepository _mappingRepository = Substitute.For<ITournamentRoundMappingRepository>();
    private readonly IFootballDataService _footballData = Substitute.For<IFootballDataService>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();

    private readonly CreateSeasonCommandHandler _handler;

    public CreateSeasonCommandHandlerTests()
    {
        _handler = new CreateSeasonCommandHandler(
            _seasonRepository, _competitionRepository, _leagueRepository, _roundRepository,
            _mappingRepository, _footballData, _mediator, _currentUserService,
            new TestDateTimeProvider(FixedNow), NullLogger<CreateSeasonCommandHandler>.Instance);

        _seasonRepository.CreateAsync(Arg.Any<Season>(), Arg.Any<CancellationToken>())
            .Returns(ci => WithId(ci.Arg<Season>(), CreatedSeasonId));

        // The database assigns the round id, and AddPlaceholderMatch refuses to work without one.
        var nextRoundId = 1;
        _roundRepository.CreateAsync(Arg.Any<Round>(), Arg.Any<CancellationToken>())
            .Returns(ci => WithId(ci.Arg<Round>(), nextRoundId++));

        _mediator.Send(Arg.Any<FetchAllTeamsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TeamDto>());
    }

    /// <summary>Stands in for the identity the database assigns on insert.</summary>
    private static T WithId<T>(T entity, int id)
    {
        typeof(T).GetProperty("Id")!.SetValue(entity, id);
        return entity;
    }

    private Competition GivenCompetition(bool isTournament = false, int? apiLeagueId = null)
    {
        var competition = new Competition(
            id: CompetitionId, code: "PREM", name: "Premier League",
            type: isTournament ? CompetitionType.Tournament : CompetitionType.League,
            logoUrl: null, description: null, apiLeagueId: apiLeagueId, createdAtUtc: FixedNow.AddYears(-1));

        _competitionRepository.GetByIdAsync(CompetitionId, Arg.Any<CancellationToken>()).Returns(competition);
        return competition;
    }

    private void GivenApiAgrees(int roundCount = 38)
    {
        _footballData.GetLeagueSeasonDetailsAsync(ApiLeagueId, SeasonStart.Year, Arg.Any<CancellationToken>())
            .Returns(new ApiSeason { Start = SeasonStart, End = SeasonEnd });
        _footballData.GetRoundsForSeasonAsync(ApiLeagueId, SeasonStart.Year, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, roundCount).Select(i => $"Regular Season - {i}").ToList());
        _footballData.GetTeamsForSeasonAsync(ApiLeagueId, SeasonStart.Year, Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private static CreateSeasonCommand Command(
        int numberOfRounds = 38,
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null,
        decimal? passStandardPrice = null,
        List<TournamentRoundMappingDto>? mappings = null) =>
        new("2026/27 Premier League", startDateUtc ?? SeasonStart, endDateUtc ?? SeasonEnd,
            "creator-1", IsActive: true, numberOfRounds, CompetitionId, passStandardPrice,
            mappings ?? []);

    private Task<SeasonDto> HandleAsync(CreateSeasonCommand? command = null) =>
        _handler.Handle(command ?? Command(), CancellationToken.None);

    // ---------- guards ----------

    [Fact]
    public async Task Handle_ShouldRequireAnAdministrator()
    {
        GivenCompetition();

        await HandleAsync();

        _currentUserService.Received(1).EnsureAdministrator();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheCompetitionDoesNotExist()
    {
        _competitionRepository.GetByIdAsync(CompetitionId, Arg.Any<CancellationToken>()).Returns((Competition?)null);

        var act = () => HandleAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    // ---------- validation against the football API ----------

    [Fact]
    public async Task Handle_ShouldSkipApiValidation_ForACompetitionWithNoApiLeague()
    {
        GivenCompetition();

        await HandleAsync();

        await _footballData.DidNotReceiveWithAnyArgs().GetLeagueSeasonDetailsAsync(default, default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheApiHasNoSuchSeason()
    {
        GivenCompetition(apiLeagueId: ApiLeagueId);
        _footballData.GetLeagueSeasonDetailsAsync(ApiLeagueId, SeasonStart.Year, Arg.Any<CancellationToken>())
            .Returns((ApiSeason)null!);

        var act = () => HandleAsync();

        (await act.Should().ThrowAsync<ValidationException>())
            .WithMessage("*returned no season data*");
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheStartDateDisagreesWithTheApi()
    {
        GivenCompetition(apiLeagueId: ApiLeagueId);
        GivenApiAgrees();

        var act = () => HandleAsync(Command(startDateUtc: SeasonStart.AddDays(1)));

        (await act.Should().ThrowAsync<ValidationException>()).WithMessage("*Start Date does not match*");
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheEndDateDisagreesWithTheApi()
    {
        GivenCompetition(apiLeagueId: ApiLeagueId);
        GivenApiAgrees();

        var act = () => HandleAsync(Command(endDateUtc: SeasonEnd.AddDays(-3)));

        (await act.Should().ThrowAsync<ValidationException>()).WithMessage("*End Date does not match*");
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheRoundCountDisagreesWithTheApi()
    {
        GivenCompetition(apiLeagueId: ApiLeagueId);
        GivenApiAgrees(roundCount: 38);

        var act = () => HandleAsync(Command(numberOfRounds: 34));

        (await act.Should().ThrowAsync<ValidationException>()).WithMessage("*Number of Rounds does not match*");
    }

    [Fact]
    public async Task Handle_ShouldRefuse_AndNameEveryTeamMissingFromTheDatabase()
    {
        // Creating the season without them would leave fixtures unmappable later.
        GivenCompetition(apiLeagueId: ApiLeagueId);
        GivenApiAgrees();
        _footballData.GetTeamsForSeasonAsync(ApiLeagueId, SeasonStart.Year, Arg.Any<CancellationToken>())
            .Returns([
                new TeamResponse { Team = new ApiTeam { Id = 1, Name = "Arsenal" } },
                new TeamResponse { Team = new ApiTeam { Id = 2, Name = "Chelsea" } }
            ]);

        var act = () => HandleAsync();

        (await act.Should().ThrowAsync<ValidationException>())
            .WithMessage("*Arsenal, Chelsea*");
    }

    [Fact]
    public async Task Handle_ShouldAccept_WhenEveryApiTeamAlreadyExists()
    {
        GivenCompetition(apiLeagueId: ApiLeagueId);
        GivenApiAgrees();
        _footballData.GetTeamsForSeasonAsync(ApiLeagueId, SeasonStart.Year, Arg.Any<CancellationToken>())
            .Returns([new TeamResponse { Team = new ApiTeam { Id = 1, Name = "Arsenal" } }]);
        _mediator.Send(Arg.Any<FetchAllTeamsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<TeamDto> { new(1, "Arsenal", "Arsenal", "logo", "ARS", 1) });

        var act = () => HandleAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_ShouldSkipDateAndRoundChecks_ForATournament()
    {
        // A tournament's later stages have no fixed dates or round names yet, so those checks
        // would reject a perfectly valid setup.
        GivenCompetition(isTournament: true, apiLeagueId: ApiLeagueId);
        GivenApiAgrees();

        var act = () => HandleAsync(Command(numberOfRounds: 7, endDateUtc: SeasonEnd.AddDays(-10)));

        await act.Should().NotThrowAsync();
        await _footballData.DidNotReceiveWithAnyArgs().GetRoundsForSeasonAsync(default, default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldExplainItself_WhenTheApiIsUnreachable()
    {
        GivenCompetition(apiLeagueId: ApiLeagueId);
        _footballData.GetLeagueSeasonDetailsAsync(ApiLeagueId, SeasonStart.Year, Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("401 Unauthorized"));

        var act = () => HandleAsync();

        (await act.Should().ThrowAsync<ValidationException>())
            .WithMessage("*Could not retrieve data from the football API*");
    }

    // ---------- what gets created ----------

    [Fact]
    public async Task Handle_ShouldCreateTheSeasonAndItsOfficialPublicLeague()
    {
        GivenCompetition();

        var result = await HandleAsync();

        await _seasonRepository.Received(1).CreateAsync(Arg.Any<Season>(), Arg.Any<CancellationToken>());
        await _leagueRepository.Received(1).CreateAsync(
            Arg.Is<League>(l => l.SeasonId == CreatedSeasonId), Arg.Any<CancellationToken>());
        result.Id.Should().Be(CreatedSeasonId);
        result.CompetitionName.Should().Be("Premier League");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Handle_ShouldStoreNoPrice_WhenThePassIsFree(int? price)
    {
        // A null price is what keeps RequiresPayment false, so a blank or zero must become null
        // rather than being stored as 0.
        GivenCompetition();

        var result = await HandleAsync(Command(passStandardPrice: price));

        result.PassStandardPrice.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldKeepAPositivePassPrice()
    {
        GivenCompetition();

        var result = await HandleAsync(Command(passStandardPrice: 12.50m));

        result.PassStandardPrice.Should().Be(12.50m);
    }

    [Fact]
    public async Task Handle_ShouldTriggerAFixtureSync_WhenTheCompetitionIsLinkedToTheApi()
    {
        GivenCompetition(apiLeagueId: ApiLeagueId);
        GivenApiAgrees();

        await HandleAsync();

        await _mediator.Received(1).Send(
            Arg.Is<SyncSeasonWithApiCommand>(c => c.SeasonId == CreatedSeasonId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotTriggerASync_ForACompetitionWithNoApiLeague()
    {
        GivenCompetition();

        await HandleAsync();

        await _mediator.DidNotReceive().Send(Arg.Any<SyncSeasonWithApiCommand>(), Arg.Any<CancellationToken>());
    }

    // ---------- tournament placeholder rounds ----------

    private static TournamentRoundMappingDto Mapping(int roundNumber, int expectedMatches, params TournamentStage[] stages) =>
        new() { RoundNumber = roundNumber, DisplayName = $"Round {roundNumber}", Stages = stages.ToList(), ExpectedMatchCount = expectedMatches };

    [Fact]
    public async Task Handle_ShouldNotCreatePlaceholderRounds_ForANonTournament()
    {
        GivenCompetition();

        await HandleAsync(Command(mappings: [Mapping(1, 4, TournamentStage.QuarterFinals)]));

        await _roundRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldNotCreatePlaceholderRounds_WhenATournamentHasNoMappings()
    {
        GivenCompetition(isTournament: true);

        await HandleAsync(Command(mappings: []));

        await _mappingRepository.DidNotReceiveWithAnyArgs().ReplaceAllForSeasonAsync(default, default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldCreateAPlaceholderRoundPerMapping()
    {
        GivenCompetition(isTournament: true);

        await HandleAsync(Command(mappings: [
            Mapping(1, 4, TournamentStage.QuarterFinals),
            Mapping(2, 2, TournamentStage.SemiFinals)
        ]));

        await _mappingRepository.Received(1).ReplaceAllForSeasonAsync(
            CreatedSeasonId, Arg.Is<List<TournamentRoundMapping>>(m => m.Count == 2), Arg.Any<CancellationToken>());
        await _roundRepository.Received(2).CreateAsync(Arg.Any<Round>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFillEachRoundWithItsExpectedNumberOfPlaceholderMatches()
    {
        GivenCompetition(isTournament: true);
        var created = new List<Round>();
        await _roundRepository.UpdateAsync(Arg.Do<Round>(created.Add), Arg.Any<CancellationToken>());

        await HandleAsync(Command(mappings: [Mapping(1, 4, TournamentStage.QuarterFinals)]));

        created.Should().ContainSingle();
        created[0].Matches.Should().HaveCount(4);
    }

    [Fact]
    public async Task Handle_ShouldNumberPlaceholderMatchesContinuouslyAcrossRounds()
    {
        // Match numbers are the season-wide ordering players see, so they must not restart per round.
        GivenCompetition(isTournament: true);
        var created = new List<Round>();
        await _roundRepository.UpdateAsync(Arg.Do<Round>(created.Add), Arg.Any<CancellationToken>());

        await HandleAsync(Command(mappings: [
            Mapping(1, 2, TournamentStage.SemiFinals),
            Mapping(2, 1, TournamentStage.Final)
        ]));

        created.SelectMany(r => r.Matches).Select(m => m.MatchNumber).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Handle_ShouldSpreadACombinedRoundAcrossItsStages()
    {
        GivenCompetition(isTournament: true);
        var created = new List<Round>();
        await _roundRepository.UpdateAsync(Arg.Do<Round>(created.Add), Arg.Any<CancellationToken>());

        await HandleAsync(Command(mappings: [
            Mapping(1, 4, TournamentStage.SemiFinals, TournamentStage.ThirdPlace, TournamentStage.Final)
        ]));

        var apiRoundNames = created.Single().Matches.Select(m => m.ApiRoundName).ToList();
        apiRoundNames.Should().HaveCount(4);
        apiRoundNames.Distinct().Should().HaveCount(3, "semi-finals, third place and final are distinct stages");
    }
}
