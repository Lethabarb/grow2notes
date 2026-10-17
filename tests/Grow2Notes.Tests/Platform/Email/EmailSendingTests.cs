using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform.Email;

/// <summary>
/// Which email sender the app has (<see cref="EmailSending"/>), read from the app's own services. The tests host the
/// app without a database, so they run without Docker, and send nothing to Communication Services: building its client
/// and its credential calls nothing, and the one request made goes to a stand-in for it.
/// </summary>
public sealed class EmailSendingTests
{
    // No such resource exists, and nothing here sends to it.
    private const string Endpoint = "https://acs-grow2notes-example.australia.communication.azure.com/";

    private const string SenderAddress = "DoNotReply@00000000-0000-0000-0000-000000000003.azurecomm.net";

    private const string ClientId = "00000000-0000-0000-0000-000000000001";

    private static readonly (string Key, string Value)[] Settings =
    [
        ("Email:Endpoint", Endpoint),
        ("Email:SenderAddress", SenderAddress),
        ("ManagedIdentity:ClientId", ClientId),
    ];

    // Development is local development; Production is what App Service runs, and what CI's end-to-end run runs.
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Without_an_endpoint_the_email_goes_to_the_console(string environment)
    {
        await using var app = new AppWithoutDatabase(environment);

        Assert.IsType<ConsoleEmailSender>(app.Services.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public async Task With_an_endpoint_the_client_signs_in_as_the_app_identity_and_holds_no_key()
    {
        await using var factory = new AppWithoutDatabase("Production");
        await using var app = WithSettings(factory, Settings);

        var sender = Assert.IsType<CommunicationServicesEmailSender>(app.Services.GetRequiredService<IEmailSender>());

        // A client made from a connection string or a key holds an AzureKeyCredential instead.
        var reachable = Reachable(sender);
        var credential = Assert.IsType<ManagedIdentityCredential>(Assert.Single(
            reachable, item => item is TokenCredential or AzureKeyCredential or AzureNamedKeyCredential));
        Assert.Equal($"ClientId {ClientId}", ManagedIdentities.IdentityOf(credential));
        Assert.Contains(new Uri(Endpoint), reachable.OfType<Uri>());
        Assert.Contains(SenderAddress, reachable.OfType<string>());
    }

    // Communication Services refuses a send over its limits with a 429 and a Retry-After, which the SDK's default
    // retries would wait out 3 times, holding the request and its transaction open long past the SPA's 10-second
    // timeout (T01's Notes). The client's own retry policy, in a pipeline to a stand-in for the service, sends once.
    [Fact]
    public async Task With_an_endpoint_the_client_tries_a_send_once_and_waits_5_seconds_at_most_for_its_answer()
    {
        await using var factory = new AppWithoutDatabase("Production");
        await using var app = WithSettings(factory, Settings);
        var sender = Assert.IsType<CommunicationServicesEmailSender>(app.Services.GetRequiredService<IEmailSender>());
        var reachable = Reachable(sender);

        var service = new ThrottlingService();
        var pipeline = new HttpPipeline(
            new HttpClientTransport(service), [Assert.Single(reachable.OfType<RetryPolicy>())]);
        using var message = pipeline.CreateMessage();
        message.Request.Uri.Reset(new Uri(Endpoint));
        await pipeline.SendAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal((429, 1), (message.Response.Status, service.Requests));

        // Azure.Core does not say publicly how long a client waits for an answer. Its internal ResponseBodyPolicy holds
        // it, so this reads that; if an Azure.Core update moves it, the test fails rather than passing unchecked.
        var responseBody = Assert.Single(
            reachable, item => item.GetType().FullName == "Azure.Core.Pipeline.ResponseBodyPolicy");
        var networkTimeout = responseBody.GetType()
            .GetField("_networkTimeout", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(responseBody);
        Assert.Equal(TimeSpan.FromSeconds(5), Assert.IsType<TimeSpan>(networkTimeout));
    }

    [Theory]
    [InlineData("ManagedIdentity:ClientId")]
    [InlineData("Email:SenderAddress")]
    public async Task An_endpoint_without_the_other_setting_stops_the_app_starting(string missing)
    {
        await using var factory = new AppWithoutDatabase("Production");
        await using var app = WithSettings(factory, [.. Settings.Where(setting => setting.Key != missing)]);

        var exception = Assert.Throws<InvalidOperationException>(() => app.Services);
        Assert.Contains($"Email:Endpoint is set but {missing} is not", exception.Message);
    }

    // Host settings rather than app configuration: Program.cs reads these while it registers services, and
    // WebApplicationFactory hands host settings to the app before Program.cs runs, but adds app configuration only
    // when the host is built.
    private static WebApplicationFactory<Program> WithSettings(
        WebApplicationFactory<Program> factory, (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    // Everything reachable from root through the fields of the Azure SDK's own types, of arrays and of structs, which
    // is how a client holds its pipeline and the pipeline its policies, the one that signs requests among them. It
    // stops at a credential and at .NET's own classes, such as HttpClient, which hold nothing of the SDK's.
    private static List<object> Reachable(object root)
    {
        var reachable = new List<object>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object>([root]);
        while (pending.TryPop(out var item))
        {
            var type = item.GetType();
            if (!type.IsValueType && !seen.Add(item))
            {
                continue;
            }

            reachable.Add(item);
            if (item is Array array)
            {
                foreach (var element in array)
                {
                    if (element is not null)
                    {
                        pending.Push(element);
                    }
                }

                continue;
            }

            var holdsSdkParts = item == root
                || (type.IsValueType && !type.IsPrimitive && !type.IsEnum)
                || type.Namespace?.StartsWith("Azure", StringComparison.Ordinal) == true;
            if (!holdsSdkParts || item is TokenCredential)
            {
                continue;
            }

            for (var declaring = type; declaring is not null; declaring = declaring.BaseType)
            {
                foreach (var field in declaring.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.GetValue(item) is { } value)
                    {
                        pending.Push(value);
                    }
                }
            }
        }

        return reachable;
    }

    // Answers as Communication Services answers a send over its limits: 429, with when to try again, here a second, so
    // that a client that retried would fail the test in moments rather than minutes.
    private sealed class ThrottlingService : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            return Task.FromResult(response);
        }
    }
}
