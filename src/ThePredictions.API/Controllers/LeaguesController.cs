using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Features.Boosts.Queries;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Features.Prizes.Queries;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Contracts.Boosts;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Contracts.Payouts;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Leagues - Create, join, and manage prediction leagues")]
public class LeaguesController(IMediator mediator) : ApiControllerBase
{
    #region Create

    [HttpPost("create")]
    [SwaggerOperation(
        Summary = "Create a new prediction league",
        Description = "Creates a new league with the specified settings. The creating user automatically becomes the league administrator and an approved member. Returns the league details including the generated 6-character entry code.")]
    [SwaggerResponse(201, "League created successfully", typeof(LeagueDto))]
    [SwaggerResponse(400, "Validation failed - invalid name, scoring settings, or season")]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<IActionResult> CreateLeagueAsync(
        [FromBody, SwaggerParameter("League configuration including name, visibility, and scoring rules", Required = true)] CreateLeagueRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateLeagueCommand(
            request.Name,
            request.SeasonId,
            request.Price,
            CurrentUserId,
            request.EntryDeadlineUtc,
            request.PointsForExactScore,
            request.PointsForCorrectResult,
            request.BankAccountName,
            request.BankSortCode,
            request.BankAccountNumber,
            request.PaymentReferenceTemplate,
            request.PrizeScheme);

        var newLeague = await mediator.Send(command, cancellationToken);

        return CreatedAtAction("GetLeagueById", new { leagueId = newLeague.Id }, newLeague);
    }

    #endregion

    #region Read

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get user's leagues for management",
        Description = "Returns all leagues where the current user is an approved member, with management details.")]
    [SwaggerResponse(200, "Leagues retrieved successfully", typeof(ManageLeaguesDto))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<ActionResult<ManageLeaguesDto>> GetManageLeaguesAsync(CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole(nameof(ApplicationUserRole.Administrator));
        var query = new GetManageLeaguesQuery(CurrentUserId, isAdmin);

        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("{leagueId:int}")]
    [SwaggerOperation(
        Summary = "Get league details",
        Description = "Returns detailed information about a specific league including settings, scoring rules, and the current user's membership status.")]
    [SwaggerResponse(200, "League details retrieved successfully", typeof(LeagueDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<LeagueDto>> GetLeagueByIdAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetLeagueByIdQuery(leagueId, CurrentUserId);
        var result = await mediator.Send(query, cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("{leagueId:int}/members")]
    [SwaggerOperation(
        Summary = "Get league members",
        Description = "Returns all members of the league including their status (approved, pending, rejected) and join date. Only approved members can view this.")]
    [SwaggerResponse(200, "Members retrieved successfully", typeof(LeagueMembersPageDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<LeagueMembersPageDto>> FetchLeagueMembersAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new FetchLeagueMembersQuery(leagueId, CurrentUserId);
        var result = await mediator.Send(query, cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("create-data")]
    [SwaggerOperation(
        Summary = "Get league creation form data",
        Description = "Returns data needed to populate the league creation form including available seasons and default scoring values.")]
    [SwaggerResponse(200, "Form data retrieved successfully", typeof(CreateLeaguePageData))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<ActionResult<CreateLeaguePageData>> GetCreateLeaguePageDataAsync(CancellationToken cancellationToken)
    {
        var query = new GetCreateLeaguePageDataQuery();
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("{leagueId:int}/prizes")]
    [SwaggerOperation(
        Summary = "Get league prize settings",
        Description = "Returns the prize distribution configuration for the league including round prizes, monthly prizes, overall prizes, and most exact scores prizes.")]
    [SwaggerResponse(200, "Prize settings retrieved successfully", typeof(LeaguePrizesPageDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<LeaguePrizesPageDto>> GetLeaguePrizesPageAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetLeaguePrizesPageQuery(leagueId, CurrentUserId);
        var result = await mediator.Send(query, cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("{leagueId:int}/rounds/{roundId:int}/results")]
    [SwaggerOperation(
        Summary = "Get round results for league",
        Description = "Returns detailed results for a specific round including each member's predictions, points scored, and ranking. Shows actual match scores and individual prediction breakdowns.")]
    [SwaggerResponse(200, "Round results retrieved successfully", typeof(IEnumerable<PredictionResultDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League or round not found")]
    public async Task<ActionResult<IEnumerable<PredictionResultDto>>> GetLeagueDashboardRoundResultsAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        [SwaggerParameter("Round identifier")] int roundId,
        CancellationToken cancellationToken)
    {
        var query = new GetLeagueDashboardRoundResultsQuery(leagueId, roundId, CurrentUserId);
        var result = await mediator.Send(query, cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("{leagueId:int}/rounds-for-dashboard")]
    [SwaggerOperation(
        Summary = "Get rounds for league dashboard",
        Description = "Returns a summary of rounds for the league dashboard including completed, in-progress, and upcoming rounds with basic stats.")]
    [SwaggerResponse(200, "Rounds retrieved successfully", typeof(IEnumerable<RoundDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<IEnumerable<RoundDto>>> GetLeagueRoundsForDashboardAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetLeagueRoundsForDashboardQuery(leagueId, CurrentUserId);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("{leagueId:int}/dashboard-data")]
    [SwaggerOperation(
        Summary = "Get comprehensive league dashboard data",
        Description = "Returns all data needed for the league dashboard page including recent results, standings, upcoming fixtures, and user's prediction status.")]
    [SwaggerResponse(200, "Dashboard data retrieved successfully", typeof(LeagueDashboardDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<LeagueDashboardDto>> GetLeagueDashboardAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole(nameof(ApplicationUserRole.Administrator));
        var query = new GetLeagueDashboardQuery(leagueId, CurrentUserId, isAdmin);
        var result = await mediator.Send(query, cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    #region Dashboard

    [HttpGet("{leagueId:int}/months")]
    [SwaggerOperation(
        Summary = "Get months with completed rounds",
        Description = "Returns a list of months that have completed rounds for monthly leaderboard filtering. Only months with at least one completed round are included.")]
    [SwaggerResponse(200, "Months retrieved successfully", typeof(IEnumerable<MonthDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<IEnumerable<MonthDto>>> GetMonthsForLeagueAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetMonthsForLeagueQuery(leagueId, CurrentUserId);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("{leagueId:int}/boost-usage")]
    [SwaggerOperation(
        Summary = "Get boost usage summary for league",
        Description = "Returns boost usage data for all members showing remaining uses per boost window. Respects deadline visibility: other players' current-round boosts are hidden until the deadline passes.")]
    [SwaggerResponse(200, "Boost usage summary retrieved successfully", typeof(List<BoostUsageSummaryDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    public async Task<ActionResult<List<BoostUsageSummaryDto>>> GetBoostUsageSummaryAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetLeagueBoostUsageSummaryQuery(leagueId, CurrentUserId);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("{leagueId:int}/leaderboard/overall")]
    [SwaggerOperation(
        Summary = "Get overall season leaderboard",
        Description = "Returns the league leaderboard ranked by total points accumulated across all completed rounds in the season.")]
    [SwaggerResponse(200, "Leaderboard retrieved successfully", typeof(IEnumerable<LeaderboardEntryDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<IEnumerable<LeaderboardEntryDto>>> GetOverallLeaderboardAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetOverallLeaderboardQuery(leagueId, CurrentUserId);
        var result = await mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{leagueId:int}/leaderboard/monthly/{month:int}")]
    [SwaggerOperation(
        Summary = "Get monthly leaderboard",
        Description = "Returns the league leaderboard for a specific month, ranked by points accumulated in rounds completed during that month.")]
    [SwaggerResponse(200, "Monthly leaderboard retrieved successfully", typeof(IEnumerable<LeaderboardEntryDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found or no data for specified month")]
    public async Task<ActionResult<IEnumerable<LeaderboardEntryDto>>> GetMonthlyLeaderboardAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        [SwaggerParameter("Month number (1-12)")] int month,
        CancellationToken cancellationToken)
    {
        var query = new GetMonthlyLeaderboardQuery(leagueId, month, CurrentUserId);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("{leagueId:int}/leaderboard/exact-scores")]
    [SwaggerOperation(
        Summary = "Get exact scores leaderboard",
        Description = "Returns the league leaderboard ranked by number of exact score predictions (where predicted score exactly matched actual score).")]
    [SwaggerResponse(200, "Exact scores leaderboard retrieved successfully", typeof(ExactScoresLeaderboardDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<ExactScoresLeaderboardDto>> GetExactScoresLeaderboardAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetExactScoresLeaderboardQuery(leagueId, CurrentUserId);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    #endregion

    #region Winnings

    [HttpGet("{leagueId:int}/winnings")]
    [SwaggerOperation(
        Summary = "Get league winnings",
        Description = "Returns all prize payouts that have been awarded in this league, including round winners, monthly winners, and special prizes.")]
    [SwaggerResponse(200, "Winnings retrieved successfully", typeof(WinningsDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<WinningsDto>> GetWinningsAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetWinningsQuery(leagueId, CurrentUserId);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    #endregion

    #region Season Recap

    [HttpGet("{leagueId:int}/season-recap")]
    [SwaggerOperation(
        Summary = "Get end-of-season recap for current user",
        Description = "Returns the logged-in user's personal performance summary for the finished season: final position, winnings, profit/loss, and supporting stats.")]
    [SwaggerResponse(200, "Recap retrieved successfully", typeof(SeasonRecapDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<SeasonRecapDto>> GetSeasonRecapAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetSeasonRecapQuery(leagueId, CurrentUserId);
        var result = await mediator.Send(query, cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("{leagueId:int}/records")]
    [SwaggerOperation(
        Summary = "Get cross-member records for the league",
        Description = "Returns league-wide highlights for the finished season: top/lowest single round, most exact scores in a round, champion, top earner, most rounds/months won, and headline trivia stats.")]
    [SwaggerResponse(200, "Records retrieved successfully", typeof(LeagueRecordsDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<LeagueRecordsDto>> GetLeagueRecordsAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetLeagueRecordsQuery(leagueId, CurrentUserId);
        var result = await mediator.Send(query, cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    #endregion

    #endregion

    #region Update

    [HttpPut("{leagueId:int}/update")]
    [SwaggerOperation(
        Summary = "Update league settings",
        Description = "Updates league configuration. Only the league administrator can perform this action. Scoring rules cannot be changed after predictions have been submitted.")]
    [SwaggerResponse(204, "League updated successfully")]
    [SwaggerResponse(400, "Validation failed or scoring rules locked")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not the league administrator")]
    [SwaggerResponse(404, "League not found")]
    public async Task<IActionResult> UpdateLeagueAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        [FromBody, SwaggerParameter("Updated league settings", Required = true)] UpdateLeagueRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateLeagueCommand(
            leagueId,
            request.Name,
            request.Price,
            request.EntryDeadlineUtc,
            request.PointsForExactScore,
            request.PointsForCorrectResult,
            CurrentUserId,
            request.BankAccountName,
            request.BankSortCode,
            request.BankAccountNumber,
            request.PaymentReferenceTemplate);

        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpGet("{leagueId:int}/payment-info")]
    [SwaggerOperation(
        Summary = "Get peer-to-peer entry-fee payment details",
        Description = "Returns the league's bank details, the entry amount and a payment reference for the requesting user. Available only to the league administrator and its members; the platform never handles the money.")]
    [SwaggerResponse(200, "Payment information returned", typeof(LeaguePaymentInfoDto))]
    [SwaggerResponse(401, "Not authenticated, or not the administrator/a member of the league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<LeaguePaymentInfoDto>> GetLeaguePaymentInfoAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var paymentInfo = await mediator.Send(new GetLeaguePaymentInfoQuery(leagueId, CurrentUserId), cancellationToken);
        return Ok(paymentInfo);
    }

    [HttpGet("{leagueId:int}/bank-details")]
    [SwaggerOperation(
        Summary = "Get decrypted bank details for editing",
        Description = "Returns the league's decrypted bank details to pre-fill the edit form. League administrator only.")]
    [SwaggerResponse(200, "Bank details returned", typeof(LeagueBankDetailsDto))]
    [SwaggerResponse(401, "Not authenticated, or not the league administrator")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<LeagueBankDetailsDto>> GetLeagueBankDetailsAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var bankDetails = await mediator.Send(new GetLeagueBankDetailsQuery(leagueId, CurrentUserId), cancellationToken);
        return Ok(bankDetails);
    }

    [HttpGet("{leagueId:int}/payouts")]
    [SwaggerOperation(
        Summary = "Get the league's end-of-season payouts",
        Description = "Returns one row per winner (total + live breakdown), their shared payout details if any, and paid state. League administrator only; mark-as-paid is available once the season is complete.")]
    [SwaggerResponse(200, "Payouts returned", typeof(LeaguePayoutsDto))]
    [SwaggerResponse(401, "Not authenticated, or not the league administrator")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<LeaguePayoutsDto>> GetLeaguePayoutsAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var payouts = await mediator.Send(new GetLeaguePayoutsQuery(leagueId, CurrentUserId), cancellationToken);
        return Ok(payouts);
    }

    [HttpPost("{leagueId:int}/payouts/{winnerUserId}/mark-paid")]
    [SwaggerOperation(
        Summary = "Mark a winner's payout as paid",
        Description = "Records that the league administrator has paid a winner their winnings. Only available once the season is complete.")]
    [SwaggerResponse(204, "Payout marked as paid")]
    [SwaggerResponse(400, "Season not complete, or the player has no winnings")]
    [SwaggerResponse(401, "Not authenticated, or not the league administrator")]
    [SwaggerResponse(404, "League not found")]
    public async Task<IActionResult> MarkLeaguePayoutPaidAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        [SwaggerParameter("Winner's user id")] string winnerUserId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new MarkLeaguePayoutPaidCommand(leagueId, winnerUserId, CurrentUserId), cancellationToken);
        return NoContent();
    }

    [HttpPost("join")]
    [SwaggerOperation(
        Summary = "Join league with entry code",
        Description = "Submits a request to join a private league using a 6-character entry code. For public leagues, membership is instant. For private leagues, the request is pending until approved by an administrator.")]
    [SwaggerResponse(200, "Join request submitted successfully", typeof(JoinLeagueResultDto))]
    [SwaggerResponse(400, "Invalid entry code or already a member")]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<ActionResult<JoinLeagueResultDto>> JoinLeagueAsync(
        [FromBody, SwaggerParameter("Entry code for the league", Required = true)] JoinLeagueRequest request,
        CancellationToken cancellationToken)
    {
        var command = new JoinLeagueCommand(CurrentUserId, CurrentUserFirstName, CurrentUserLastName, null, request.EntryCode);
        var leagueId = await mediator.Send(command, cancellationToken);

        return Ok(new JoinLeagueResultDto(leagueId));
    }

    [HttpPost("{leagueId:int}/join")]
    [SwaggerOperation(
        Summary = "Join public league directly",
        Description = "Joins a public league directly without an entry code. Only works for public leagues.")]
    [SwaggerResponse(200, "Joined league successfully", typeof(JoinLeagueResultDto))]
    [SwaggerResponse(400, "League is private or already a member")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<JoinLeagueResultDto>> JoinPublicLeagueAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var command = new JoinLeagueCommand(CurrentUserId, CurrentUserFirstName, CurrentUserLastName, leagueId, null);
        var joinedLeagueId = await mediator.Send(command, cancellationToken);

        return Ok(new JoinLeagueResultDto(joinedLeagueId));
    }

    [HttpPost("{leagueId:int}/members/{memberId}/status")]
    [SwaggerOperation(
        Summary = "Update member status",
        Description = "Approves, rejects, or removes a league member. Only the league administrator can perform this action.")]
    [SwaggerResponse(204, "Member status updated successfully")]
    [SwaggerResponse(400, "Invalid status transition")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not the league administrator")]
    [SwaggerResponse(404, "League or member not found")]
    public async Task<IActionResult> UpdateLeagueMemberStatusAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        [SwaggerParameter("Member identifier")] string memberId,
        [FromBody, SwaggerParameter("New membership status", Required = true)] LeagueMemberStatus newStatus,
        CancellationToken cancellationToken)
    {
        var command = new UpdateLeagueMemberStatusCommand(leagueId, memberId, CurrentUserId, newStatus);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("{leagueId:int}/prizes")]
    [SwaggerOperation(
        Summary = "Update league prize settings",
        Description = "Configures prize distribution for the league. Only the league administrator can perform this action.")]
    [SwaggerResponse(204, "Prize settings updated successfully")]
    [SwaggerResponse(400, "Invalid prize configuration")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not the league administrator")]
    [SwaggerResponse(404, "League not found")]
    public async Task<IActionResult> DefinePrizeStructureAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        [FromBody, SwaggerParameter("Prize distribution configuration", Required = true)] DefinePrizeStructureRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DefinePrizeStructureCommand(leagueId, CurrentUserId, request.PrizeSettings);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpGet("{leagueId:int}/prize-breakdown")]
    [SwaggerOperation(
        Summary = "Get the live projected prize breakdown",
        Description = "Returns the projected, round-number prize breakdown at the current entrant count. Members and the administrator only. Finalises at the entry deadline.")]
    [SwaggerResponse(200, "Breakdown retrieved successfully", typeof(PrizeBreakdownDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<PrizeBreakdownDto>> GetPrizeBreakdownAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetLeaguePrizeBreakdownQuery(leagueId, CurrentUserId), cancellationToken));
    }

    [HttpGet("{leagueId:int}/prize-preview")]
    [SwaggerOperation(
        Summary = "Preview a league's prizes before joining",
        Description = "Returns headline facts, the projected breakdown if you join, and the attributed +£x effect of your own entry. Numbers and the organiser's name only. Private leagues require the entry code.")]
    [SwaggerResponse(200, "Preview retrieved successfully", typeof(PrizePreviewDto))]
    [SwaggerResponse(401, "Not authenticated, or a valid entry code is required")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<PrizePreviewDto>> GetPrizePreviewAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        [FromQuery, SwaggerParameter("Entry code (required for private leagues)")] string? entryCode,
        CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetPrizePreviewQuery(leagueId, entryCode), cancellationToken));
    }

    [HttpPost("evaluate-scheme")]
    [SwaggerOperation(
        Summary = "Preview a draft prize scheme",
        Description = "Evaluates a draft scheme at a hypothetical entrant count, for the create/edit editor's live derived-prize preview.")]
    [SwaggerResponse(200, "Breakdown computed successfully", typeof(PrizeBreakdownDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(404, "Season not found")]
    public async Task<ActionResult<PrizeBreakdownDto>> EvaluateSchemeAsync(
        [FromBody, SwaggerParameter("Draft scheme and context", Required = true)] EvaluateSchemeRequest request,
        CancellationToken cancellationToken)
    {
        var query = new EvaluateSchemeQuery(request.SeasonId, request.Price, request.EntrantCount, request.Scheme);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpPut("{leagueId:int}/prize-scheme")]
    [SwaggerOperation(
        Summary = "Set the league's prize scheme",
        Description = "Sets the up-front prize scheme (categories and per-entry allocation). Write-once: a league administrator may set it while unset; thereafter only a site administrator can override it.")]
    [SwaggerResponse(204, "Prize scheme set successfully")]
    [SwaggerResponse(400, "Invalid scheme, or the scheme is already set")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not permitted to set the scheme")]
    [SwaggerResponse(404, "League not found")]
    public async Task<IActionResult> SetPrizeSchemeAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        [FromBody, SwaggerParameter("Prize scheme configuration", Required = true)] PrizeSchemeRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new SetPrizeSchemeCommand(leagueId, CurrentUserId, request), cancellationToken);

        return NoContent();
    }

    [HttpDelete("{leagueId:int}/join-request")]
    [SwaggerOperation(
        Summary = "Withdraw join request",
        Description = "Cancels a pending request to join a league. Only the user who submitted the request can withdraw it.")]
    [SwaggerResponse(204, "Join request withdrawn successfully")]
    [SwaggerResponse(400, "No pending request found")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(404, "League not found")]
    public async Task<IActionResult> CancelJoinRequestAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var command = new CancelLeagueRequestCommand(leagueId, CurrentUserId);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPut("{leagueId:int}/dismiss-alert")]
    [SwaggerOperation(
        Summary = "Dismiss league alert",
        Description = "Marks an alert or notification for the league as dismissed for the current user.")]
    [SwaggerResponse(204, "Alert dismissed successfully")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League or alert not found")]
    public async Task<IActionResult> DismissAlertAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var command = new DismissRejectedNotificationCommand(leagueId, CurrentUserId);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPut("{leagueId:int}/archive")]
    [SwaggerOperation(
        Summary = "Archive league for current user",
        Description = "Hides the league from the current user's My Leagues carousel by default. Only available to approved members.")]
    [SwaggerResponse(204, "League archived successfully")]
    [SwaggerResponse(400, "League cannot be archived in its current state")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(404, "League membership not found")]
    public async Task<IActionResult> ArchiveLeagueAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var command = new SetLeagueArchivedCommand(leagueId, CurrentUserId, IsArchived: true);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPut("{leagueId:int}/unarchive")]
    [SwaggerOperation(
        Summary = "Unarchive league for current user",
        Description = "Restores a previously archived league to the current user's My Leagues carousel.")]
    [SwaggerResponse(204, "League unarchived successfully")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(404, "League membership not found")]
    public async Task<IActionResult> UnarchiveLeagueAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var command = new SetLeagueArchivedCommand(leagueId, CurrentUserId, IsArchived: false);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    #endregion

    #region Delete

    [HttpDelete("{leagueId:int}")]
    [SwaggerOperation(
        Summary = "Delete league",
        Description = "Permanently deletes a league. Only the league administrator can perform this action, and only if they are the sole member.")]
    [SwaggerResponse(204, "League deleted successfully")]
    [SwaggerResponse(400, "Cannot delete - league has other members")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not the league administrator")]
    [SwaggerResponse(404, "League not found")]
    public async Task<IActionResult> DeleteLeagueAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole(nameof(ApplicationUserRole.Administrator));

        var command = new DeleteLeagueCommand(leagueId, CurrentUserId, isAdmin);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    #endregion
}
