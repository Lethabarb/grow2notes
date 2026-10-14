using System.Reflection;
using Azure;
using Azure.Core;
using Azure.Identity;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform.Email;

/// <summary>
/// Which email sender the app has (<see cref="EmailSending"/>), read from the app's own services. The tests host the
/// app without a database, so they run without Docker, and send nothing: building Communication Services' client and
/// its credential calls nothing.
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
}
