using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Services;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeasonsController : ControllerBase
{
    private readonly ISeasonService _seasonService;

    public SeasonsController(ISeasonService seasonService)
    {
        _seasonService = seasonService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllSeasons()
    {
        var seasons = await _seasonService.GetAllAsync();
        return Ok(seasons);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetSeasonById(int id)
    {
        var season = await _seasonService.GetByIdAsync(id);
        if (season == null)
        {
            return NotFound();
        }
        return Ok(season);
    }
}