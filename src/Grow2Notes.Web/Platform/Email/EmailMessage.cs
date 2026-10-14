namespace Grow2Notes.Web.Platform.Email;

/// <summary>An email to one address, in plain text only, with no HTML, image or tracking.</summary>
/// <param name="To">The address it goes to.</param>
/// <param name="Subject">The subject line.</param>
/// <param name="Text">The body, as plain text.</param>
internal sealed record EmailMessage(string To, string Subject, string Text);
