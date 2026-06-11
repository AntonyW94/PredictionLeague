namespace ThePredictions.Application.Features.External.Tasks.Queries;

/// <summary>An approved league member who has not yet received the league welcome email.</summary>
public record LeagueWelcomeRecipient(
    string UserId,
    string Email,
    string FirstName);
