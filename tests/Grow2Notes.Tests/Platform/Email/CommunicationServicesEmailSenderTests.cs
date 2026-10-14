using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Azure.Communication.Email;
using Azure.Core;
using Azure.Core.Pipeline;
using Grow2Notes.Web.Platform.Email;
using EmailMessage = Grow2Notes.Web.Platform.Email.EmailMessage;

namespace Grow2Notes.Tests.Platform.Email;

/// <summary>
/// What the Communication Services sender asks of the service, answered by a stand-in for it in place of the network,
/// so nothing is sent.
/// </summary>
public sealed class CommunicationServicesEmailSenderTests
{
    // No such resource exists: the stand-in answers in its place.
    private const string Endpoint = "https://acs-grow2notes-example.australia.communication.azure.com/";

    private const string SenderAddress = "DoNotReply@00000000-0000-0000-0000-000000000003.azurecomm.net";

    [Fact]
    public async Task Sends_one_plain_text_message_from_the_sender_address_and_returns_once_it_is_accepted()
    {
        var service = new AcceptingService();
        var client = new EmailClient(
            new Uri(Endpoint), new StandInCredential(),
            new EmailClientOptions { Transport = new HttpClientTransport(service) });
        var sender = new CommunicationServicesEmailSender(client, SenderAddress);

        await sender.SendAsync(
            new EmailMessage("sam.taylor@example.org", "A subject", "A first line\n\nA second line"),
            TestContext.Current.CancellationToken);

        // One request: the sender never asks after the message's delivery.
        var (method, uri, body) = Assert.Single(service.Requests);
        Assert.Equal((HttpMethod.Post, "/emails:send"), (method, uri.AbsolutePath));
        var email = JsonNode.Parse(body)!.AsObject();

        // No HTML, attachments, reply-to address or headers, and only the one recipient.
        Assert.Equal(
            ["content", "recipients", "senderAddress", "userEngagementTrackingDisabled"],
            email.Select(member => member.Key).Order());
        Assert.Equal(SenderAddress, email["senderAddress"]?.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(
            new JsonObject { ["subject"] = "A subject", ["plainText"] = "A first line\n\nA second line" },
            email["content"]));
        var recipients = email["recipients"]!.AsObject();
        Assert.Equal(["to"], recipients.Select(member => member.Key));
        Assert.Equal(
            "sam.taylor@example.org", Assert.Single(recipients["to"]!.AsArray())?["address"]?.GetValue<string>());
        Assert.True(email["userEngagementTrackingDisabled"]?.GetValue<bool>());
    }

    // Stands in for the app's identity, which the tests have not got.
    private sealed class StandInCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("stand-in-token", DateTimeOffset.MaxValue);

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new(GetToken(requestContext, cancellationToken));
    }

    // Answers as Communication Services answers a message it has accepted for delivery: 202, with where to ask after
    // it, which a sender that waited for delivery would then ask.
    private sealed class AcceptingService : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((
                request.Method,
                request.RequestUri!,
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            var response = new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(
                    """{"id":"operation-1","status":"Running"}""", Encoding.UTF8, "application/json"),
            };
            response.Headers.Add("Operation-Location", $"{Endpoint}emails/operations/operation-1");
            return response;
        }
    }
}
