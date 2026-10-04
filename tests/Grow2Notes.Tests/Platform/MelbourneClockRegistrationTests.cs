using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform;

[Collection<SqlServerCollection>]
public sealed class MelbourneClockRegistrationTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    [Fact]
    public void The_app_provides_TimeProvider_and_MelbourneClock()
    {
        Assert.NotNull(factory.Services.GetService<TimeProvider>());
        Assert.NotNull(factory.Services.GetService<MelbourneClock>());
    }
}
