using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Core.Repositories;

namespace PredictionLeague.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SeasonsController : ControllerBase
    {
        private readonly ISeasonRepository _seasonRepository;

        public SeasonsController(ISeasonRepository seasonRepository)
        {
            _seasonRepository = seasonRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllSeasons()
        {
            var seasons = await _seasonRepository.GetAllAsync();
            return Ok(seasons);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSeasonById(int id)
        {
            var season = await _seasonRepository.GetByIdAsync(id);
            if (season == null)
                return NotFound();

            return Ok(season);
        }
    }
}