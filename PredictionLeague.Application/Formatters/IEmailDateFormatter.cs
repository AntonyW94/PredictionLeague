namespace PredictionLeague.Application.Formatters;

public interface IEmailDateFormatter
{
    string FormatDeadline(DateTime dateUtc);
}