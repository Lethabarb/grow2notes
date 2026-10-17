using Grow2Notes.Web.Platform.Email;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// The setup-link email (design.md §8.1 step 2), the only email the app sends (A23), with the wording that S00.02.07
/// proposed, approved as proposed (D85). An invite and a sign-in reset send the same one, so it names no one, and its
/// last line fits both. Its text is built from the link alone, so no name, participant data or other personal detail
/// can reach it, and the only name in it is Grow2Notes (D42).
/// </summary>
internal static class SetupLinkEmail
{
    public const string Subject = "Set up your Grow2Notes sign-in";

    /// <summary>The email to <paramref name="address"/> that carries <paramref name="setupLink"/>.</summary>
    public static EmailMessage To(string address, string setupLink) => new(address, Subject, $"""
        Open this link to set up how you sign in to Grow2Notes:

        {setupLink}

        The link works once and lasts 7 days. If it has expired, ask a manager to send a new one.

        You are getting this email because a manager added you to Grow2Notes or reset your sign-in.
        """);

    /// <summary>
    /// Sends the email to <paramref name="address"/> that carries <paramref name="setupLink"/> through
    /// <paramref name="sender"/>, returning once it has been handed on. Its caller sends it inside the transaction that
    /// made the link, after the audit event and before the commit, so that a send that fails leaves nothing changed
    /// (T01's Notes).
    /// </summary>
    /// <exception cref="EmailNotSentException">
    /// The sender could not hand the email on, and threw anything but the caller's own cancellation, which is thrown
    /// as it came.
    /// </exception>
    public static async Task SendAsync(
        IEmailSender sender, string address, string setupLink, CancellationToken cancellationToken)
    {
        try
        {
            await sender.SendAsync(To(address, setupLink), cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw new EmailNotSentException(exception.GetType().FullName!);
        }
    }
}
