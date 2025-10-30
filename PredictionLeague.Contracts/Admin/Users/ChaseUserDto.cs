namespace PredictionLeague.Contracts.Admin.Users;

public record ChaseUserDto(string Email, string FirstName, string RoundName, DateTime Deadline, string UserId);