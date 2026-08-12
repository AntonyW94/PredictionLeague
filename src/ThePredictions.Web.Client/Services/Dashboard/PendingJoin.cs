namespace ThePredictions.Web.Client.Services.Dashboard;

/// <summary>
/// A league somebody was trying to join when they were told they needed a Season Pass first.
/// </summary>
/// <remarks>
/// Held so that buying the pass can offer to take them straight back to it rather than leaving them to find it again.
/// One of the two identifiers is set: a public league is joined by id, a private one by the code they typed - which is why
/// the code is kept in memory for the trip rather than put in the address bar.
/// </remarks>
public sealed record PendingJoin(int? LeagueId, string? EntryCode, string LeagueName);
