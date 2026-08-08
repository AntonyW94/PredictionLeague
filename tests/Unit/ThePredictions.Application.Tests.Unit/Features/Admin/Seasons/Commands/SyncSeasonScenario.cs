using NSubstitute;
using ThePredictions.Application.Features.Admin.Seasons.Commands;
using ThePredictions.Application.FootballApi.DTOs;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Commands;

/// <summary>
/// Shared scaffolding for the fixture sync. Both the league and tournament paths load a season, a
/// competition, the API's fixtures and the existing rounds, so the arrange step has the same shape
/// each time.
/// </summary>
internal sealed class SyncSeasonScenario
{
    public const int SeasonId = 11;
    public const int CompetitionId = 3;
    public const int ApiLeagueId = 39;
    public const int HomeTeamId = 101;
    public const int AwayTeamId = 102;
    public const int ApiHomeTeamId = 1;
    public const int ApiAwayTeamId = 2;

    public static readonly DateTime SeasonStart = new(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);

    public ISeasonRepository Seasons { get; } = Substitute.For<ISeasonRepository>();
    public ICompetitionRepository Competitions { get; } = Substitute.For<ICompetitionRepository>();
    public ITeamRepository Teams { get; } = Substitute.For<ITeamRepository>();
    public IRoundRepository Rounds { get; } = Substitute.For<IRoundRepository>();
    public ITournamentRoundMappingRepository Mappings { get; } = Substitute.For<ITournamentRoundMappingRepository>();
    public IFootballDataService FootballData { get; } = Substitute.For<IFootballDataService>();
    public IMediator Mediator { get; } = Substitute.For<IMediator>();

    private int _nextRoundId = 900;

    public SyncSeasonScenario()
    {
        GivenSeason();
        GivenCompetition();
        GivenTeamsKnown();
        GivenRounds();
        GivenApiRoundNames();
        GivenApiFixtures();
        Mappings.GetBySeasonIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns([]);
        Rounds.GetMatchIdsWithPredictionsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>()).Returns([]);
        Rounds.CreateAsync(Arg.Any<Round>(), Arg.Any<CancellationToken>())
            .Returns(ci => WithId(ci.Arg<Round>(), _nextRoundId++));
    }

    public SyncSeasonWithApiCommandHandler BuildHandler() =>
        new(Seasons, Competitions, Teams, Rounds, Mappings, FootballData, Mediator,
            NullLogger<SyncSeasonWithApiCommandHandler>.Instance);

    public Task HandleAsync() =>
        BuildHandler().Handle(new SyncSeasonWithApiCommand(SeasonId), CancellationToken.None);

    /// <summary>Stands in for the identity the database assigns on insert.</summary>
    public static T WithId<T>(T entity, int id)
    {
        typeof(T).GetProperty("Id")!.SetValue(entity, id);
        return entity;
    }

    // ---------- arrange helpers ----------

    public void GivenSeason() =>
        Seasons.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(
            new Season(id: SeasonId, name: "2026/27", startDateUtc: SeasonStart,
                endDateUtc: SeasonStart.AddMonths(9), isActive: true, numberOfRounds: 38,
                competitionId: CompetitionId, passStandardPrice: null, passPremiumPrice: null));

    public void GivenNoSeason() =>
        Seasons.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns((Season?)null);

    public void GivenCompetition(bool isTournament = false, int? apiLeagueId = ApiLeagueId) =>
        Competitions.GetByIdAsync(CompetitionId, Arg.Any<CancellationToken>()).Returns(
            new Competition(id: CompetitionId, code: "PREM", name: "Premier League",
                type: isTournament ? CompetitionType.Tournament : CompetitionType.League,
                logoUrl: null, description: null, apiLeagueId: apiLeagueId,
                createdAtUtc: SeasonStart.AddYears(-1)));

    public void GivenNoCompetition() =>
        Competitions.GetByIdAsync(CompetitionId, Arg.Any<CancellationToken>()).Returns((Competition?)null);

    public void GivenTeamsKnown(params (int ApiId, int LocalId)[] teams)
    {
        // A round rejects two matches between the same pair, so the default map covers enough
        // teams for several fixtures to sit in one round.
        var map = (teams.Length == 0
                ? Enumerable.Range(1, 10).Select(i => (ApiId: i, LocalId: 100 + i)).ToArray()
                : teams)
            .ToDictionary(
                t => t.ApiId,
                t => new Team(id: t.LocalId, name: $"Team {t.LocalId}", shortName: $"T{t.LocalId}",
                    logoUrl: "logo", abbreviation: $"T{t.LocalId}", apiTeamId: t.ApiId));

        // The ids arrive as a lazy LINQ query. The real repository enumerates them to build its
        // query, so the stub must too - otherwise the filtering that produces them never runs.
        Teams.GetByApiIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _ = call.Arg<IEnumerable<int>>().ToList();
                return map;
            });
    }

    public void GivenNoTeamsKnown() =>
        Teams.GetByApiIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _ = call.Arg<IEnumerable<int>>().ToList();
                return new Dictionary<int, Team>();
            });

    public void GivenRounds(params Round[] rounds) =>
        Rounds.GetAllForSeasonAsync(SeasonId, Arg.Any<CancellationToken>())
            .Returns(rounds.ToDictionary(r => r.Id));

    public void GivenApiRoundNames(params string[] names) =>
        FootballData.GetRoundsForSeasonAsync(ApiLeagueId, SeasonStart.Year, Arg.Any<CancellationToken>())
            .Returns(names.ToList());

    public void GivenApiFixtures(params FixtureResponse[] fixtures) =>
        FootballData.GetAllFixturesForSeasonAsync(ApiLeagueId, SeasonStart.Year, Arg.Any<CancellationToken>())
            .Returns(fixtures.ToList());

    public void GivenMappings(params TournamentRoundMapping[] mappings) =>
        Mappings.GetBySeasonIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(mappings.ToList());

    public void GivenMatchesWithPredictions(params int[] matchIds) =>
        Rounds.GetMatchIdsWithPredictionsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(matchIds.ToList());

    // ---------- builders ----------

    public static string RoundName(int number) => $"Regular Season - {number}";

    public static FixtureResponse Fixture(
        int externalId,
        DateTime matchDateTimeUtc,
        string apiRoundName,
        string status = "NS",
        int apiHomeTeamId = ApiHomeTeamId,
        int apiAwayTeamId = ApiAwayTeamId,
        bool withTeams = true,
        bool withFixture = true,
        bool withRoundName = true,
        bool withAwayTeam = true) =>
        new()
        {
            Fixture = withFixture
                ? new Fixture { Id = externalId, Date = new DateTimeOffset(matchDateTimeUtc), Status = new Status { Short = status } }
                : null,
            League = withRoundName ? new ApiLeague { Id = ApiLeagueId, RoundName = apiRoundName } : null,
            Teams = withTeams
                // Qualified: the Admin.Teams test namespace would otherwise shadow this DTO here.
                ? new FootballApi.DTOs.Teams
                {
                    Home = new ApiTeam { Id = apiHomeTeamId, Name = "Home" },
                    Away = withAwayTeam ? new ApiTeam { Id = apiAwayTeamId, Name = "Away" } : null!
                }
                : null
        };

    public static Round Round(
        int id,
        int roundNumber,
        DateTime startDateUtc,
        string? apiRoundName = null,
        RoundStatus status = RoundStatus.Draft,
        params Match[] matches) =>
        new(id: id, seasonId: SeasonId, roundNumber: roundNumber, displayName: $"Gameweek {roundNumber}",
            startDateUtc: startDateUtc, deadlineUtc: startDateUtc.AddMinutes(-30), status: status,
            apiRoundName: apiRoundName ?? RoundName(roundNumber), lastReminderSentUtc: null,
            matches: matches.Length == 0 ? null : matches);

    public static Match Match(
        int id,
        int roundId,
        DateTime matchDateTimeUtc,
        int? externalId,
        MatchStatus status = MatchStatus.Scheduled,
        int? homeTeamId = HomeTeamId,
        int? awayTeamId = AwayTeamId,
        string? apiRoundName = null,
        string? placeholderHome = null,
        string? placeholderAway = null) =>
        new(id: id, roundId: roundId, homeTeamId: homeTeamId, awayTeamId: awayTeamId,
            matchDateTimeUtc: matchDateTimeUtc, customLockTimeUtc: null, status: status,
            actualHomeTeamScore: null, actualAwayTeamScore: null, externalId: externalId,
            matchNumber: 1, placeholderHomeName: placeholderHome, placeholderAwayName: placeholderAway,
            apiRoundName: apiRoundName);
}
