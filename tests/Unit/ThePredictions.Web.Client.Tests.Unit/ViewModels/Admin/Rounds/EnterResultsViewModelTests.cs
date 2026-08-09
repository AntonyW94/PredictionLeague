using System.Net;
using FluentAssertions;
using ThePredictions.Contracts.Admin.Matches;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Web.Client.Tests.Unit.TestDoubles;
using ThePredictions.Web.Client.ViewModels.Admin.Rounds;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.ViewModels.Admin.Rounds;

/// <summary>
/// The admin enter-results screen. It was excluded as "properties only, no logic to test" while owning
/// three things a person notices when they break: whether the screen shows a spinner or a form, what it
/// says when the round will not load, and whether saving actually reports failure rather than looking
/// like it worked. The last one matters most - an admin who believes results saved when they did not
/// leaves a round unscored.
/// </summary>
public class EnterResultsViewModelTests
{
    private const int RoundId = 7;
    private const int SeasonId = 3;

    private readonly StubHttpMessageHandler _handler = new();
    private readonly TestNavigationManager _navigationManager = new();
    private readonly EnterResultsViewModel _viewModel;

    public EnterResultsViewModelTests()
    {
        _viewModel = new EnterResultsViewModel(
            new HttpClient(_handler) { BaseAddress = new Uri("https://localhost/") },
            _navigationManager);
    }

    private static RoundDetailsDto RoundDetails(params int[] matchIds) => new()
    {
        Round = new RoundDto(RoundId, SeasonId, RoundNumber: 12, "Regular Season - 12",
            new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 15, 14, 30, 0, DateTimeKind.Utc),
            RoundStatus.InProgress, matchIds.Length),
        Matches = matchIds.Select(id => new MatchInRoundDto(
            id, new DateTime(2026, 8, 15, 15, 0, 0, DateTimeKind.Utc), 1,
            10, "Arsenal", "ARS", "ARS", null,
            11, "Chelsea", "CHE", "CHE", null,
            null, null, MatchStatus.Scheduled)).ToList()
    };

    // ---------- loading the round ----------

    [Fact]
    public async Task LoadRoundDetails_ShouldPopulateTheMatchesAndRoundNumber()
    {
        _handler.EnqueueJson(HttpStatusCode.OK, RoundDetails(101, 102));

        await _viewModel.LoadRoundDetails(RoundId);

        _viewModel.RoundNumber.Should().Be(12);
        _viewModel.Matches.Select(m => m.MatchId).Should().Equal(101, 102);
    }

    [Fact]
    public async Task LoadRoundDetails_ShouldClearTheLoadingFlag_WhenTheRoundLoads()
    {
        _handler.EnqueueJson(HttpStatusCode.OK, RoundDetails(101));

        await _viewModel.LoadRoundDetails(RoundId);

        _viewModel.IsLoading.Should().BeFalse();
        _viewModel.ErrorMessage.Should().BeNull();
    }

    // The screen must not sit on a spinner for ever when the round cannot be fetched.
    [Fact]
    public async Task LoadRoundDetails_ShouldReportAnError_AndStopLoading_WhenTheRequestFails()
    {
        _handler.EnqueueStatus(HttpStatusCode.InternalServerError);

        await _viewModel.LoadRoundDetails(RoundId);

        _viewModel.ErrorMessage.Should().Be("Could not load round details.");
        _viewModel.IsLoading.Should().BeFalse();
        _viewModel.Matches.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadRoundDetails_ShouldLeaveTheScreenEmpty_WhenTheRoundComesBackAsNothing()
    {
        _handler.EnqueueJson(HttpStatusCode.OK, (RoundDetailsDto?)null!);

        await _viewModel.LoadRoundDetails(RoundId);

        _viewModel.Matches.Should().BeEmpty();
        _viewModel.RoundNumber.Should().Be(0);
        _viewModel.IsLoading.Should().BeFalse();
    }

    // A retry after a failure must clear the previous error, or the screen shows a stale one for ever.
    [Fact]
    public async Task LoadRoundDetails_ShouldClearAPreviousError_WhenRetried()
    {
        _handler.EnqueueStatus(HttpStatusCode.InternalServerError);
        await _viewModel.LoadRoundDetails(RoundId);

        _handler.EnqueueJson(HttpStatusCode.OK, RoundDetails(101));
        await _viewModel.LoadRoundDetails(RoundId);

        _viewModel.ErrorMessage.Should().BeNull();
        _viewModel.Matches.Should().ContainSingle();
    }

    // ---------- saving results ----------

    [Fact]
    public async Task HandleSaveResultsAsync_ShouldPostTheEnteredScores()
    {
        _handler.EnqueueJson(HttpStatusCode.OK, RoundDetails(101, 102));
        await _viewModel.LoadRoundDetails(RoundId);
        _viewModel.Matches[0].UpdateScore(isHomeTeam: true, delta: 2);

        _handler.EnqueueStatus(HttpStatusCode.OK);
        await _viewModel.HandleSaveResultsAsync(RoundId);

        var save = _handler.Requests.Last();
        save.Method.Should().Be(HttpMethod.Put);
        save.Uri!.AbsolutePath.Should().Be($"/api/admin/rounds/{RoundId}/results");
        save.HasContent.Should().BeTrue();
    }

    [Fact]
    public async Task HandleSaveResultsAsync_ShouldReportSuccessAndReturnToTheDashboard()
    {
        _handler.EnqueueJson(HttpStatusCode.OK, RoundDetails(101));
        await _viewModel.LoadRoundDetails(RoundId);

        _handler.EnqueueStatus(HttpStatusCode.OK);
        await _viewModel.HandleSaveResultsAsync(RoundId);

        _viewModel.SuccessMessage.Should().Be("Results saved and points calculated successfully!");
        _viewModel.ErrorMessage.Should().BeNull();
        _navigationManager.LastNavigatedTo.Should().Be("/dashboard");
        _viewModel.IsBusy.Should().BeFalse();
    }

    // The one that matters: a failed save must say so and stay put, not report success and navigate
    // away leaving the round unscored.
    [Fact]
    public async Task HandleSaveResultsAsync_ShouldReportFailureAndStayPut_WhenTheSaveIsRejected()
    {
        _handler.EnqueueJson(HttpStatusCode.OK, RoundDetails(101));
        await _viewModel.LoadRoundDetails(RoundId);

        _handler.EnqueueStatus(HttpStatusCode.BadRequest);
        await _viewModel.HandleSaveResultsAsync(RoundId);

        _viewModel.ErrorMessage.Should().Be("There was an error saving the results.");
        _viewModel.SuccessMessage.Should().BeNull();
        _navigationManager.LastNavigatedTo.Should().BeNull();
        _viewModel.IsBusy.Should().BeFalse();
    }

    // A second attempt must not still be showing the last one's outcome.
    [Fact]
    public async Task HandleSaveResultsAsync_ShouldClearThePreviousOutcome_WhenSavingAgain()
    {
        _handler.EnqueueJson(HttpStatusCode.OK, RoundDetails(101));
        await _viewModel.LoadRoundDetails(RoundId);

        _handler.EnqueueStatus(HttpStatusCode.BadRequest);
        await _viewModel.HandleSaveResultsAsync(RoundId);

        _handler.EnqueueStatus(HttpStatusCode.OK);
        await _viewModel.HandleSaveResultsAsync(RoundId);

        _viewModel.ErrorMessage.Should().BeNull();
        _viewModel.SuccessMessage.Should().NotBeNull();
    }

    // ---------- going back ----------

    // The season id is only known once the round has loaded, which is why this is not a plain link.
    [Fact]
    public async Task BackToRounds_ShouldReturnToTheSeasonTheRoundBelongsTo()
    {
        _handler.EnqueueJson(HttpStatusCode.OK, RoundDetails(101));
        await _viewModel.LoadRoundDetails(RoundId);

        _viewModel.BackToRounds();

        _navigationManager.LastNavigatedTo.Should().Be($"/admin/seasons/{SeasonId}/rounds");
    }
}
