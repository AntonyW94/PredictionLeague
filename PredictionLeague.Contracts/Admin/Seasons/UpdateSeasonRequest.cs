namespace PredictionLeague.Contracts.Admin.Seasons;

public class UpdateSeasonRequest : BaseSeasonRequest
{
    public bool IsActive { get; set; }
}