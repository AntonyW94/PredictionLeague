namespace ThePredictions.Application.Services;

/// <summary>
/// The selected "data picker" user whose details seed the smart defaults on the email-test form.
/// Note this is never the recipient - test emails always go to the calling admin.
/// </summary>
public record EmailTestUserData(string FirstName, string LastName, string Email);
