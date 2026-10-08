using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform;

[Collection<SqlServerCollection>]
public sealed class TenantContextRegistrationTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    [Fact]
    public void The_app_provides_one_TenantContext_per_scope()
    {
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();

        var tenant = Assert.IsType<TenantContext>(first.ServiceProvider.GetService<ITenantContext>());
        Assert.Same(tenant, first.ServiceProvider.GetService<ITenantContext>());
        Assert.NotSame(tenant, second.ServiceProvider.GetService<ITenantContext>());
    }
}
