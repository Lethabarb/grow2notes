using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Features.Users;
using Grow2Notes.Web.Platform.Email;

namespace Grow2Notes.Tests.Features.Users;

/// <summary>
/// The setup-link email (design.md §8.1 step 2, A23), in the wording approved as proposed (D85), and its sending, a
/// failure of which reaches the logs as the sender's exception type alone (§9.5).
/// </summary>
public sealed class SetupLinkEmailTests
{
    private const string Address = "sam.taylor@example.org";

    // In the form that the setup-link step makes, with an escaped token.
    private const string SetupLink =
        "https://grow2notes.example/setup#u=0b5c2f0e-8d61-4a8e-9a50-2f3f8f3f8a11&t=CfDJ8Abc%2Bdef%2Fghi%3D";

    [Fact]
    public void Is_the_approved_subject_and_plain_text_with_the_link_on_its_own_line_and_no_other_value()
    {
        var email = SetupLinkEmail.To(Address, SetupLink);

        // The address is only where the email goes: no name, address or anything else but the link is in the text.
        Assert.Equal(
            new EmailMessage(
                Address,
                "Set up your Grow2Notes sign-in",
                """
                Open this link to set up how you sign in to Grow2Notes:

                https://grow2notes.example/setup#u=0b5c2f0e-8d61-4a8e-9a50-2f3f8f3f8a11&t=CfDJ8Abc%2Bdef%2Fghi%3D

                The link works once and lasts 7 days. If it has expired, ask a manager to send a new one.

                You are getting this email because a manager added you to Grow2Notes or reset your sign-in.
                """),
            email);
    }

    [Fact]
    public async Task Sending_hands_the_email_to_the_sender()
    {
        var sender = new RecordingEmailSender();

        await SetupLinkEmail.SendAsync(sender, Address, SetupLink, TestContext.Current.CancellationToken);

        Assert.Equal([SetupLinkEmail.To(Address, SetupLink)], sender.Sent);
    }

    // The sender's exception goes no further, not even as the inner exception, which a logger would write too.
    [Fact]
    public async Task A_sender_that_fails_gives_an_exception_naming_its_exception_s_type_with_no_address_or_link()
    {
        var notSent = await Assert.ThrowsAsync<EmailNotSentException>(() => SetupLinkEmail.SendAsync(
            new FailingEmailSender(), Address, SetupLink, TestContext.Current.CancellationToken));

        Assert.Equal("The email was not sent: the sender threw System.InvalidOperationException.", notSent.Message);
        Assert.Null(notSent.InnerException);
        Assert.DoesNotContain(Address, notSent.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SetupLink, notSent.ToString(), StringComparison.Ordinal);
    }

    // As when the client goes while the email is being handed on: nobody waits for an answer.
    [Fact]
    public async Task The_caller_s_own_cancellation_is_thrown_as_it_came()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() => SetupLinkEmail.SendAsync(
            new CancelledEmailSender(), Address, SetupLink, cancellation.Token));
    }

    private sealed class CancelledEmailSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            Task.FromCanceled(cancellationToken);
    }
}
