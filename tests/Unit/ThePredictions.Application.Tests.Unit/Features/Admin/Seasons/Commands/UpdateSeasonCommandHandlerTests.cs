using FluentAssertions;
using FluentValidation;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ThePredictions.Application.Features.Admin.Seasons.Commands;
using ThePredictions.Application.FootballApi.DTOs;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Commands;

/// <summary>
/// Editing a season re-runs the same checks against the football feed that creating one does, so an
/// admin cannot quietly move a season away from the data it will be synced with.
/// </summary>
public class UpdateSeasonCommandHandlerTests
{
    private const int SeasonId = 11;
    private const int CompetitionId = 3;
    private const int ApiLeagueId = 39;

    private static readonly DateTime SeasonStart = new(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonEnd = new(2027, 5, 23, 0, 0, 0, DateTimeKind.Utc);

    private readonly ISeasonRepository _seasons = Substitute.For<ISeasonRepository>();
    private readonly ICompetitionRepository _competitions = Substitute.For<ICompetitionRepository>();
    private readonly ITournamentRoundMappingRepository _mappings = Substitute.For<ITournamentRoundMappingRepository>();
    private readonly IFootballDataService _footballData = Substitute.For<IFootballDataService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private readonly UpdateSeasonCommandHandler _handler;

    public UpdateSeasonCommandHandlerTests()
    {
        _handler = new UpdateSeasonCommandHandler(_seasons, _competitions, _mappings, _footballData, _currentUser);
        GivenSeason();
        GivenCompetition();
    }

    private Season GivenSeason(decimal? passStandardPrice = null, decimal? passPremiumPrice = null)
    {
        // The domain refuses a premium price without a standard one, so the two travel together.
        var season = new Season(id: SeasonId, name: "2026/27", startDateUtc: SeasonStart,
            endDateUtc: SeasonEnd, isActive: true, numberOfRounds: 38, competitionId: CompetitionId,
            passStandardPrice: passStandardPrice, passPremiumPrice: passPremiumPrice);

        _seasons.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(season);
        return season;
    }

    private void GivenCompetition(bool isTournament = false, int? apiLeagueId = null) =>
        _competitions.GetByIdAsync(CompetitionId, Arg.Any<CancellationToken>()).Returns(
            new Competition(id: CompetitionId, code: "PREM", name: "Premier League",
                type: isTournament ? CompetitionType.Tournament : CompetitionType.League,
                logoUrl: null, description: null, apiLeagueId: apiLeagueId,
                createdAtUtc: SeasonStart.AddYears(-1)));

    private void GivenApiAgrees(int roundCount = 38)
    {
        _footballData.GetLeagueSeasonDetailsAsync(ApiLeagueId, SeasonStart.Year, Arg.Any<CancellationToken>())
            .Returns(new ApiSeason { Start = SeasonStart, End = SeasonEnd });
        _footballData.GetRoundsForSeasonAsync(ApiLeagueId, SeasonStart.Year, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, roundCount).Select(i => $"Regular Season - {i}").ToList());
    }

    private static UpdateSeasonCommand Command(
        string name = "2026/27 Premier League",
        int numberOfRounds = 38,
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null,
        decimal? passStandardPrice = null,
        List<TournamentRoundMappingDto>? mappings = null) =>
        new(SeasonId, name, startDateUtc ?? SeasonStart, endDateUtc ?? SeasonEnd,
            IsActive: true, numberOfRounds, CompetitionId, passStandardPrice, mappings ?? []);

    private Task HandleAsync(UpdateSeasonCommand? command = null) =>
        _handler.Handle(command ?? Command(), CancellationToken.None);

    private static TournamentRoundMappingDto Mapping(int roundNumber, params TournamentStage[] stages) =>
        new() { RoundNumber = roundNumber, DisplayName = $"Round {roundNumber}", Stages = stages.ToList(), ExpectedMatchCount = stages.Length };

    // ---------- guards ----------

    [Fact]
    public async Task Handle_ShouldRequireAnAdministrator()
    {
        await HandleAsync();

        _currentUser.Received(1).EnsureAdministrator();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheSeasonDoesNotExist()
    {
        _seasons.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns((Season?)null);

        var act = () => HandleAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheCompetitionDoesNotExist()
    {
        _competitions.GetByIdAsync(CompetitionId, Arg.Any<CancellationToken>()).Returns((Competition?)null);

        var act = () => HandleAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    // ---------- validation against the feed ----------

    [Fact]
    public async Task Handle_ShouldSkipApiValidation_ForACompetitionWithNoApiLeague()
    {
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

        (await act.Should().ThrowAsync<ValidationException>()).WithMessage("*returned no season data*");
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

        var act = () => HandleAsync(Command(endDateUtc: SeasonEnd.AddDays(-2)));

        (await act.Should().ThrowAsync<ValidationException>()).WithMessage("*End Date does not match*");
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheRoundCountDisagreesWithTheApi()
    {
        GivenCompetition(apiLeagueId: ApiLeagueId);
        GivenApiAgrees(roundCount: 38);

        var act = () => HandleAsync(Command(numberOfRounds: 20));

        (await act.Should().ThrowAsync<ValidationException>()).WithMessage("*Number of Rounds does not match*");
    }

    [Fact]
    public async Task Handle_ShouldSkipDateAndRoundChecks_ForATournament()
    {
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
            .Throws(new HttpRequestException("timeout"));

        var act = () => HandleAsync();

        (await act.Should().ThrowAsync<ValidationException>())
            .WithMessage("*Could not retrieve data from the football API*");
    }

    [Fact]
    public async Task Handle_ShouldNotSaveAnything_WhenValidationFails()
    {
        GivenCompetition(apiLeagueId: ApiLeagueId);
        GivenApiAgrees();

        var act = () => HandleAsync(Command(numberOfRounds: 20));

        await act.Should().ThrowAsync<ValidationException>();
        await _seasons.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    // ---------- what gets saved ----------

    [Fact]
    public async Task Handle_ShouldApplyTheEditedDetails()
    {
        var season = GivenSeason();

        await HandleAsync(Command(name: "Renamed Season", numberOfRounds: 34));

        season.Name.Should().Be("Renamed Season");
        season.NumberOfRounds.Should().Be(34);
        await _seasons.Received(1).UpdateAsync(season, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Handle_ShouldStoreNoPrice_WhenThePassIsMadeFree(int? price)
    {
        // Null is what keeps the pass free; storing zero would still read as "requires payment".
        var season = GivenSeason(passStandardPrice: 10m);

        await HandleAsync(Command(passStandardPrice: price));

        season.PassStandardPrice.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldKeepAPositivePassPrice()
    {
        var season = GivenSeason();

        await HandleAsync(Command(passStandardPrice: 15m));

        season.PassStandardPrice.Should().Be(15m);
    }

    [Fact]
    public async Task Handle_ShouldLeaveThePremiumPriceUntouched()
    {
        // The edit form does not expose it, so an update must not silently clear it.
        var season = GivenSeason(passStandardPrice: 10m, passPremiumPrice: 25m);

        await HandleAsync(Command(passStandardPrice: 15m));

        season.PassPremiumPrice.Should().Be(25m);
    }

    // ---------- tournament mappings ----------

    [Fact]
    public async Task Handle_ShouldReplaceTheRoundMappings_ForATournament()
    {
        GivenCompetition(isTournament: true);

        await HandleAsync(Command(mappings: [Mapping(1, TournamentStage.SemiFinals), Mapping(2, TournamentStage.Final)]));

        await _mappings.Received(1).ReplaceAllForSeasonAsync(
            SeasonId, Arg.Is<List<TournamentRoundMapping>>(m => m.Count == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotTouchMappings_ForANonTournament()
    {
        await HandleAsync(Command(mappings: [Mapping(1, TournamentStage.SemiFinals)]));

        await _mappings.DidNotReceiveWithAnyArgs().ReplaceAllForSeasonAsync(default, default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldNotWipeMappings_WhenTheEditSendsNone()
    {
        // An empty list means "not edited here", not "delete them all".
        GivenCompetition(isTournament: true);

        await HandleAsync(Command(mappings: []));

        await _mappings.DidNotReceiveWithAnyArgs().ReplaceAllForSeasonAsync(default, default!, CancellationToken.None);
    }
}
