namespace Grow2Notes.Web.Platform.Email;

/// <summary>
/// Thrown in place of whatever an <see cref="IEmailSender"/> threw when it could not hand an email on, so that what
/// reaches the logs and telemetry is the sender's exception type alone (design.md §9.5): the sender's own exception is
/// not kept, as its message can hold the address or the text, which holds the setup link, as Communication Services'
/// error text can. The exception handler logs it as any unhandled exception, and the request answers <c>500</c>.
/// </summary>
/// <param name="senderExceptionType">The full name of the type of the exception that the sender threw.</param>
internal sealed class EmailNotSentException(string senderExceptionType)
    : Exception($"The email was not sent: the sender threw {senderExceptionType}.");
