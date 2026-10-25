using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Security.KeyVault.Secrets;
using Grow2Notes.Web.Platform.Email;

namespace Grow2Notes.Tests.Platform.Email;

/// <summary>
/// What the Resend sender asks of the vault and of Resend, answered by stand-ins for both in place of the network, so
/// nothing is read from a vault or sent.
/// </summary>
public sealed class ResendEmailSenderTests
{
    // No such resource exists: the stand-in answers in its place.
    private const string VaultAddress = "https://kv-grow2notes-example.vault.azure.net/";

    private const string Tenant = "00000000-0000-0000-0000-000000000002";

    private const string SecretName = "resend-api-key";

    private const string SenderAddress = "Grow2Notes <setup@example.org>";

    private const string Recipient = "sam.taylor@example.org";

    private const string Link = "https://app.example.org/setup?token=a-token";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Sends_one_plain_text_message_from_the_sender_address_with_the_key_from_Key_Vault()
    {
        var vault = new VaultStandIn();
        var resend = new ResendStandIn(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var sender = SenderFor(vault, resend, Timeout);

        await sender.SendAsync(
            new EmailMessage(Recipient, "A subject", "A first line\n\nA second line"),
            TestContext.Current.CancellationToken);

        var request = Assert.Single(resend.Requests);
        Assert.Equal((HttpMethod.Post, ResendEmailSender.ApiAddress), (request.Method, request.Uri));
        Assert.Equal("Bearer test-api-key", request.Authorization);
        Assert.Equal("Grow2Notes", request.UserAgent);

        // No HTML, tags, headers, reply-to address or attachments, and only the one recipient.
        var email = JsonNode.Parse(request.Body)!.AsObject();
        Assert.Equal(["from", "subject", "text", "to"], email.Select(member => member.Key).Order());
        Assert.Equal(SenderAddress, email["from"]?.GetValue<string>());
        Assert.Equal("A subject", email["subject"]?.GetValue<string>());
        Assert.Equal("A first line\n\nA second line", email["text"]?.GetValue<string>());
        Assert.Equal(Recipient, Assert.Single(email["to"]!.AsArray())?.GetValue<string>());
    }

    [Fact]
    public async Task Reads_the_key_again_at_each_send()
    {
        var vault = new VaultStandIn();
        var resend = new ResendStandIn(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var sender = SenderFor(vault, resend, Timeout);
        var message = new EmailMessage(Recipient, "A subject", "A line");

        await sender.SendAsync(message, TestContext.Current.CancellationToken);
        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        // Only the requests that carry a token: the vault client's first request of each start carries none, and is
        // answered with a challenge.
        var reads = vault.Requests.Where(request => request.Authorised).ToList();
        Assert.Equal(2, reads.Count);
        Assert.All(reads, read => Assert.Equal(
            (HttpMethod.Get, $"/secrets/{SecretName}"), (read.Method, read.Path.TrimEnd('/'))));
        Assert.Equal(2, resend.Requests.Count);
    }

    // 422 is how Resend refuses a message it cannot send, and 429 how it refuses one over its limits, with when to try
    // again, which the sender does not wait out. The answer's body, which could hold the address or the link, is never
    // read, so neither reaches the exception.
    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task A_refused_send_throws_after_one_request_and_holds_nothing_of_the_answer(HttpStatusCode status)
    {
        var vault = new VaultStandIn();
        var resend = new ResendStandIn(_ =>
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(
                    $$"""{"message":"Could not send to {{Recipient}}: {{Link}}"}""",
                    Encoding.UTF8,
                    "application/json"),
            };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            return Task.FromResult(response);
        });
        var sender = SenderFor(vault, resend, Timeout);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => sender.SendAsync(
            new EmailMessage(Recipient, "A subject", $"Set up here: {Link}"), TestContext.Current.CancellationToken));

        Assert.Single(resend.Requests);
        Assert.DoesNotContain(Recipient, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Link, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_send_with_no_answer_within_its_timeout_fails()
    {
        var vault = new VaultStandIn();
        var resend = new ResendStandIn(async cancellationToken =>
        {
            await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var sender = SenderFor(vault, resend, TimeSpan.FromMilliseconds(100));
        var testToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.SendAsync(new EmailMessage(Recipient, "A subject", "A line"), testToken));

        // The deadline ended it, not the caller.
        Assert.False(testToken.IsCancellationRequested);
    }

    private static ResendEmailSender SenderFor(VaultStandIn vault, ResendStandIn resend, TimeSpan timeout)
    {
        var secrets = new SecretClient(
            new Uri(VaultAddress),
            new StandInCredential(),
            new SecretClientOptions { Transport = new HttpClientTransport(vault) });

        // No base address and no default headers, so that what the sender sends shows its own address and headers.
        return new ResendEmailSender(new HttpClient(resend), secrets, SecretName, SenderAddress, timeout);
    }

    // Stands in for the app's identity, which the tests have not got.
    private sealed class StandInCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("stand-in-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new(GetToken(requestContext, cancellationToken));
    }

    // Answers as Key Vault answers: a request with no token gets a 401 whose challenge names the tenant, and one with a
    // token gets the secret.
    private sealed class VaultStandIn : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, bool Authorised)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var authorised = request.Headers.Authorization is not null;
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, authorised));
            if (!authorised)
            {
                var challenge = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                challenge.Headers.TryAddWithoutValidation(
                    "WWW-Authenticate",
                    "Bearer authorization=\"https://login.microsoftonline.com/" + Tenant + "\", " +
                    "resource=\"https://vault.azure.net\"");
                return Task.FromResult(challenge);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"value":"test-api-key","id":"{{VaultAddress}}secrets/{{SecretName}}/v1"}""",
                    Encoding.UTF8,
                    "application/json"),
            });
        }
    }

    // Records what each request carries, then answers as the test says.
    private sealed class ResendStandIn(Func<CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri, string? Authorization, string UserAgent, string Body)>
            Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Headers.UserAgent.ToString(),
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return await answer(cancellationToken);
        }
    }
}
