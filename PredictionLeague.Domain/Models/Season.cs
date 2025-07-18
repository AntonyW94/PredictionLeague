namespace PredictionLeague.Domain.Models;

public class Season
{
    public int Id { get; init; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public bool IsActive { get; set; }

    public void SetDates(DateTime startDate, DateTime endDate)
    {
        if (endDate > startDate.AddMonths(10))
            throw new ArgumentException("A season cannot span more than 10 months.");
    
        StartDate = startDate;
        EndDate = endDate;
    }
}