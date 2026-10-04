using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

[Collection<SqlServerCollection>]
public sealed class Grow2NotesDbContextTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    [Fact]
    public async Task The_app_context_connects_with_the_Grow2Notes_connection_string()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        Assert.True(await db.Database.CanConnectAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void The_model_has_the_Identity_passkey_table_and_no_role_tables()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var tables = db.Model.GetEntityTypes().Select(e => e.GetTableName()).ToList();

        // Identity adds AspNetUserPasskeys only at schema Version3, which the context takes from the app's Identity
        // options; without it the context silently builds the older schema.
        Assert.Contains("AspNetUserPasskeys", tables);
        Assert.DoesNotContain(tables, t => t is not null && t.Contains("Role", StringComparison.Ordinal));
    }

    [Fact]
    public void Role_and_Status_are_tinyint_columns()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var user = db.Model.FindEntityType(typeof(ApplicationUser))!;

        Assert.Equal("tinyint", user.FindProperty(nameof(ApplicationUser.Role))!.GetColumnType());
        Assert.Equal("tinyint", user.FindProperty(nameof(ApplicationUser.Status))!.GetColumnType());
    }
}
