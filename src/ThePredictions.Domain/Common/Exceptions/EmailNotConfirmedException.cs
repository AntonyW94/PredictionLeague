namespace ThePredictions.Domain.Common.Exceptions;

/// <summary>
/// Thrown when a user with an unverified email attempts an action that requires confirmation
/// (e.g. acquiring a Season Pass to take part). Surfaced as 403 so the client can prompt the
/// user to confirm their email.
/// </summary>
public class EmailNotConfirmedException()
    : Exception("Please confirm your email address before taking part. Check your inbox for the confirmation link, or request a new one from your account.");
