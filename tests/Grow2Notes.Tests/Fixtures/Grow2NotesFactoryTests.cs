using System.Net;
using Grow2Notes.Web.Platform.Email;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

[Collection<SqlServerCollection>]
public sealed class Grow2NotesFactoryTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    [Fact]
    public async Task Hosts_the_app_against_the_SQL_Server_container()
    {
        using var client = factory.CreateClient();

        // The readiness check opens a connection with the app's connection string and runs a query on the server.
        using var response = await client.GetAsync("/healthz/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Records_each_email_in_place_of_sending_it()
    {
        Assert.IsType<RecordingEmailSender>(factory.Services.GetRequiredService<IEmailSender>());
    }
}
