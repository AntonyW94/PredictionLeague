using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Admin.Seasons.Commands;
using ThePredictions.Application.Features.Admin.Seasons.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Contracts.Common;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers.Admin;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/admin/[controller]")]
[SwaggerTag("Admin: Seasons - Manage competition seasons (Admin only)")]
[ExcludeFromCodeCoverage(Justification = "Controller action: forwards to MediatR and returns the result. The behaviour under test is the handler.")]
public class SeasonsController(IMediator mediator, IFootballDataService footballDataService) : ApiControllerBase
{
    #region Create

    [HttpPost("create")]
    [SwaggerOperation(
        Summary = "Create a new season",
        Description = "Creates a new competition season with the specified configuration. Links to external football API for fixture data.")]
    [SwaggerResponse(201, "Season created successfully", typeof(SeasonDto))]
    [SwaggerResponse(400, "Validation failed")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<IActionResult> CreateSeasonAsync(
        [FromBody, SwaggerParameter("Season configuration", Required = true)] CreateSeasonRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateSeasonCommand(
            request.Name,
            request.StartDateUtc,
            request.EndDateUtc,
            CurrentUserId,
            request.IsActive,
            request.NumberOfRounds,
            request.CompetitionId,
            request.PassStandardPrice,
            request.TournamentRoundMappings
        );

        var newSeasonDto = await mediator.Send(command, cancellationToken);

        return CreatedAtAction("GetById", new { seasonId = newSeasonDto.Id }, newSeasonDto);
    }

    #endregion

    #region Read

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all seasons",
        Description = "Returns all seasons in the system, both active and inactive.")]
    [SwaggerResponse(200, "Seasons retrieved successfully", typeof(IEnumerable<SeasonDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<ActionResult<IEnumerable<SeasonDto>>> FetchAllAsync(CancellationToken cancellationToken)
    {
        var query = new FetchAllSeasonsQuery();
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("{seasonId:int}")]
    [SwaggerOperation(
        Summary = "Get season by ID",
        Description = "Returns details of a specific season including configuration and linked API league.")]
    [SwaggerResponse(200, "Season retrieved successfully", typeof(SeasonDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Season not found")]
    public async Task<ActionResult<SeasonDto>> GetByIdAsync(
        [SwaggerParameter("Season identifier")] int seasonId,
        CancellationToken cancellationToken)
    {
        var query = new GetSeasonByIdQuery(seasonId);
        var season = await mediator.Send(query, cancellationToken);

        if (season == null)
            return NotFound();

        return Ok(season);
    }

    [HttpGet("{seasonId:int}/has-predictions")]
    [SwaggerOperation(
        Summary = "Check if season has predictions",
        Description = "Returns true if any match in the season has user predictions.")]
    [SwaggerResponse(200, "Check completed", typeof(bool))]
    public async Task<IActionResult> HasPredictionsAsync(
        [SwaggerParameter("Season identifier")] int seasonId,
        CancellationToken cancellationToken)
    {
        var query = new HasSeasonPredictionsQuery(seasonId);
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{seasonId:int}/pass-holders")]
    [SwaggerOperation(
        Summary = "Get a page of the Season Pass holders for a season",
        Description = "Returns one sorted, filtered page of the users who hold a Season Pass for the season, with the tier, how the pass arose, what they paid, and when, plus the money collected across the whole matching set. Page size is snapped to one of the allowed sizes and the page number is clamped into range. The acquisition bounds are exact instants rather than dates, because only the caller knows which time zone its days are measured in.")]
    [SwaggerResponse(200, "Pass holders retrieved successfully", typeof(SeasonPassHoldersPageDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Season not found")]
    public async Task<ActionResult<SeasonPassHoldersPageDto>> GetPassHoldersAsync(
        [SwaggerParameter("Season identifier")] int seasonId,
        [FromQuery, SwaggerParameter("1-based page number")] int page,
        [FromQuery, SwaggerParameter("Rows per page (5, 10, 25, 50 or 100)")] int pageSize,
        [FromQuery, SwaggerParameter("Column to sort by")] SeasonPassHolderSortField sortField,
        [FromQuery, SwaggerParameter("Direction to sort in")] SortDirection sortDirection,
        [FromQuery, SwaggerParameter("Only holders whose name contains this text")] string? nameFilter,
        [FromQuery, SwaggerParameter("Only passes acquired at or after this instant (inclusive)")] DateTime? acquiredFromUtc,
        [FromQuery, SwaggerParameter("Only passes acquired before this instant (exclusive - for a whole day, pass the instant the next day begins)")] DateTime? acquiredBeforeUtc,
        [FromQuery, SwaggerParameter("Only passes with at least this total paid")] decimal? minimumPaid,
        [FromQuery, SwaggerParameter("Only passes with at most this total paid")] decimal? maximumPaid,
        CancellationToken cancellationToken)
    {
        var query = new GetSeasonPassHoldersQuery(
            seasonId,
            page,
            pageSize,
            sortField,
            sortDirection,
            nameFilter,
            acquiredFromUtc,
            acquiredBeforeUtc,
            minimumPaid,
            maximumPaid);

        var holders = await mediator.Send(query, cancellationToken);

        if (holders == null)
            return NotFound();

        return Ok(holders);
    }

    [HttpGet("price-recommendation")]
    [SwaggerOperation(
        Summary = "Get a recommended Standard price",
        Description = "Computes an advisory Standard Season Pass price for a season draft from the business-borne running costs, length-weighted apportionment, and the last comparable season's player count. The admin can override it.")]
    [SwaggerResponse(200, "Recommendation computed", typeof(PriceRecommendationDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<ActionResult<PriceRecommendationDto>> GetPriceRecommendationAsync(
        [FromQuery, SwaggerParameter("Competition identifier")] int competitionId,
        [FromQuery, SwaggerParameter("Number of rounds in the season")] int numberOfRounds,
        [FromQuery, SwaggerParameter("Season start date (UTC)")] DateTime startDateUtc,
        [FromQuery, SwaggerParameter("Season being edited, excluded from lookups")] int? seasonId,
        CancellationToken cancellationToken)
    {
        var query = new GetSeasonPriceRecommendationQuery(competitionId, numberOfRounds, startDateUtc, seasonId);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("{seasonId:int}/tournament-mappings")]
    [SwaggerOperation(
        Summary = "Get tournament round mappings",
        Description = "Returns the configured tournament round structure for a season.")]
    [SwaggerResponse(200, "Mappings retrieved successfully", typeof(List<TournamentRoundMappingDto>))]
    public async Task<IActionResult> GetTournamentMappingsAsync(
        [SwaggerParameter("Season identifier")] int seasonId,
        CancellationToken cancellationToken)
    {
        var query = new GetTournamentRoundMappingsQuery(seasonId);
        var mappings = await mediator.Send(query, cancellationToken);
        return Ok(mappings);
    }

    [HttpGet("api-rounds")]
    [SwaggerOperation(
        Summary = "Get available API round names",
        Description = "Fetches round names from the football API and parses them into tournament stages. Used to auto-populate the tournament structure.")]
    [SwaggerResponse(200, "Stages retrieved successfully")]
    [SwaggerResponse(400, "Could not fetch round names")]
    public async Task<IActionResult> GetApiRoundsAsync(
        [FromQuery, SwaggerParameter("API League ID")] int apiLeagueId,
        [FromQuery, SwaggerParameter("Season year")] int seasonYear,
        CancellationToken cancellationToken)
    {
        try
        {
            var apiRoundNames = await footballDataService.GetRoundsForSeasonAsync(apiLeagueId, seasonYear, cancellationToken);

            var stages = apiRoundNames
                .Where(name => TournamentRoundNameParser.TryParseStage(name, out _))
                .Select(name =>
                {
                    TournamentRoundNameParser.TryParseStage(name, out var stage);
                    return stage;
                })
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            return Ok(stages);
        }
        catch (HttpRequestException)
        {
            return BadRequest("Could not fetch round names from the football API.");
        }
    }

    [HttpGet("api-league-lookup")]
    [SwaggerOperation(
        Summary = "Look up league/season details from API",
        Description = "Fetches season dates, round count, and detects competition type from the football API. Used to auto-populate the season creation form.")]
    [SwaggerResponse(200, "Lookup successful", typeof(ApiLeagueLookupResult))]
    [SwaggerResponse(400, "Could not fetch league details")]
    public async Task<IActionResult> LookupApiLeagueAsync(
        [FromQuery, SwaggerParameter("API League ID")] int apiLeagueId,
        [FromQuery, SwaggerParameter("Season year")] int seasonYear,
        CancellationToken cancellationToken)
    {
        try
        {
            var (leagueName, apiSeason) = await footballDataService.GetLeagueInfoAsync(apiLeagueId, seasonYear, cancellationToken);
            var apiRoundNames = (await footballDataService.GetRoundsForSeasonAsync(apiLeagueId, seasonYear, cancellationToken)).ToList();
            var apiTeams = (await footballDataService.GetTeamsForSeasonAsync(apiLeagueId, seasonYear, cancellationToken)).ToList();

            var tournamentStages = apiRoundNames
                .Where(name => TournamentRoundNameParser.TryParseStage(name, out _))
                .Select(name =>
                {
                    TournamentRoundNameParser.TryParseStage(name, out var stage);
                    return stage;
                })
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            var isTournament = tournamentStages.Count > 0;

            return Ok(new ApiLeagueLookupResult
            {
                LeagueName = leagueName,
                StartDateUtc = apiSeason.Start,
                EndDateUtc = apiSeason.End,
                RoundCount = apiRoundNames.Count,
                TeamCount = apiTeams.Count,
                CompetitionType = isTournament ? CompetitionType.Tournament : CompetitionType.League,
                TournamentStages = tournamentStages
            });
        }
        catch (HttpRequestException)
        {
            return BadRequest("Could not fetch league details from the football API. Check the League ID and Season Year.");
        }
    }

    #endregion

    #region Update

    [HttpPut("{seasonId:int}/update")]
    [SwaggerOperation(
        Summary = "Update season details",
        Description = "Updates an existing season's configuration including name, dates, and API linkage.")]
    [SwaggerResponse(204, "Season updated successfully")]
    [SwaggerResponse(400, "Validation failed")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Season not found")]
    public async Task<IActionResult> UpdateSeasonAsync(
        [SwaggerParameter("Season identifier")] int seasonId,
        [FromBody, SwaggerParameter("Updated season configuration", Required = true)] UpdateSeasonRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSeasonCommand(
            seasonId,
            request.Name,
            request.StartDateUtc,
            request.EndDateUtc,
            request.IsActive,
            request.NumberOfRounds,
            request.CompetitionId,
            request.PassStandardPrice,
            request.TournamentRoundMappings);

        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPut("{seasonId:int}/status")]
    [SwaggerOperation(
        Summary = "Update season active status",
        Description = "Activates or deactivates a season. Inactive seasons are hidden from users.")]
    [SwaggerResponse(204, "Status updated successfully")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Season not found")]
    public async Task<IActionResult> UpdateStatusAsync(
        [SwaggerParameter("Season identifier")] int seasonId,
        [FromBody, SwaggerParameter("New active status", Required = true)] bool isActive,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSeasonStatusCommand(seasonId, isActive);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    #endregion

    #region Delete

    [HttpDelete("{seasonId:int}")]
    [SwaggerOperation(
        Summary = "Delete a season",
        Description = "Permanently deletes a season and all associated rounds, matches, and leagues. Cannot delete a season that has predictions.")]
    [SwaggerResponse(204, "Season deleted successfully")]
    [SwaggerResponse(400, "Cannot delete - season has predictions")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "Season not found")]
    public async Task<IActionResult> DeleteSeasonAsync(
        [SwaggerParameter("Season identifier")] int seasonId,
        CancellationToken cancellationToken)
    {
        var command = new DeleteSeasonCommand(seasonId);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    #endregion
}
