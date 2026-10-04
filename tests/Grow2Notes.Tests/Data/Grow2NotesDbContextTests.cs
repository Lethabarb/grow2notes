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

    [Fact]
    public void DisplayName_and_the_columns_that_hold_the_email_address_are_sized_from_Limits()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var user = db.Model.FindEntityType(typeof(ApplicationUser))!;
        string[] emailColumns =
        [
            nameof(ApplicationUser.Email),
            nameof(ApplicationUser.NormalizedEmail),
            nameof(ApplicationUser.UserName),
            nameof(ApplicationUser.NormalizedUserName),
        ];

        Assert.Equal(Limits.DisplayName, user.FindProperty(nameof(ApplicationUser.DisplayName))!.GetMaxLength());
        Assert.All(emailColumns, name => Assert.Equal(Limits.Email, user.FindProperty(name)!.GetMaxLength()));
    }

    [Fact]
    public void NormalizedEmail_has_one_index_and_it_is_unique()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var user = db.Model.FindEntityType(typeof(ApplicationUser))!;

        // One index, not two: Identity's non-unique EmailIndex is made unique rather than joined by a second index.
        var index = Assert.Single(user.GetIndexes(),
            i => i.Properties.Any(p => p.Name == nameof(ApplicationUser.NormalizedEmail)));
        Assert.True(index.IsUnique);
        Assert.Equal([nameof(ApplicationUser.NormalizedEmail)], index.Properties.Select(p => p.Name));
    }

    [Fact]
    public void A_user_belongs_to_one_organisation_and_OrganisationId_with_Id_is_an_alternate_key()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var user = db.Model.FindEntityType(typeof(ApplicationUser))!;

        var organisation = Assert.Single(user.GetForeignKeys(),
            fk => fk.PrincipalEntityType.ClrType == typeof(Organisation));
        Assert.Equal([nameof(ApplicationUser.OrganisationId)], organisation.Properties.Select(p => p.Name));
        Assert.True(organisation.PrincipalKey.IsPrimaryKey());
        Assert.True(organisation.IsRequired);

        var alternateKey = Assert.Single(user.GetKeys(), k => !k.IsPrimaryKey());
        Assert.Equal(
            [nameof(ApplicationUser.OrganisationId), nameof(ApplicationUser.Id)],
            alternateKey.Properties.Select(p => p.Name));
    }

    [Fact]
    public void Organisation_Name_and_GuidePrompts_are_sized_from_Limits_and_GuidePrompts_defaults_to_empty()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var organisation = db.Model.FindEntityType(typeof(Organisation))!;
        var name = organisation.FindProperty(nameof(Organisation.Name))!;
        var guidePrompts = organisation.FindProperty(nameof(Organisation.GuidePrompts))!;

        Assert.Equal(Limits.OrganisationName, name.GetMaxLength());
        Assert.False(name.IsNullable);
        Assert.Equal(Limits.GuidePrompts, guidePrompts.GetMaxLength());
        Assert.False(guidePrompts.IsNullable);
        Assert.Equal("", guidePrompts.GetDefaultValue());
    }
}
