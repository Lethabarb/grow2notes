using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Tests.Data;

[Collection<SqlServerCollection>]
public sealed class KeysDbContextTests(SqlServerFixture sqlServer, Grow2NotesFactory factory)
    : IClassFixture<Grow2NotesFactory>
{
    [Fact]
    public async Task The_keys_context_connects_with_the_same_connection_string_as_the_app_context()
    {
        using var scope = factory.Services.CreateScope();
        var keys = scope.ServiceProvider.GetRequiredService<KeysDbContext>();
        var app = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        Assert.Equal(app.Database.GetConnectionString(), keys.Database.GetConnectionString());
        Assert.True(await keys.Database.CanConnectAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void The_key_ring_table_is_DataProtectionKeys_with_an_identity_key_and_the_key_XML()
    {
        using var scope = factory.Services.CreateScope();
        var keys = scope.ServiceProvider.GetRequiredService<KeysDbContext>();

        var table = KeyRingTable(keys);

        Assert.Equal(["Id"], table.PrimaryKey!.Columns.Select(c => c.Name));
        Assert.Equal(
            ["Id int NOT NULL IDENTITY", "FriendlyName nvarchar(max) NULL", "Xml nvarchar(max) NULL"],
            Describe(table));
    }

    [Fact]
    public void The_app_context_creates_the_key_ring_table_and_the_keys_context_has_nothing_to_migrate()
    {
        using var scope = factory.Services.CreateScope();
        var keys = scope.ServiceProvider.GetRequiredService<KeysDbContext>();
        var app = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        // The differences from an empty database are what each context's first migration would hold.
        Assert.Contains(Differences(app), o => o is CreateTableOperation { Name: "DataProtectionKeys" });
        Assert.Empty(Differences(keys));
        Assert.Equal(Describe(KeyRingTable(keys)), Describe(KeyRingTable(app)));
    }

    [Fact]
    public void Data_Protection_keeps_its_keys_through_the_keys_context_under_the_Grow2Notes_name()
    {
        var keyManagement = factory.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        var dataProtection = factory.Services.GetRequiredService<IOptions<DataProtectionOptions>>().Value;

        Assert.IsType<EntityFrameworkCoreXmlRepository<KeysDbContext>>(keyManagement.XmlRepository);
        Assert.Equal("Grow2Notes", dataProtection.ApplicationDiscriminator);
    }

    [Fact]
    public async Task A_payload_protected_by_one_instance_of_the_app_is_unprotected_by_another()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // A database of the test's own, whose key ring starts empty. In the shared one, any instance of the app that
        // started earlier in the run has already made a key.
        var database = await sqlServer.CreateDatabaseAsync("KeyRingRoundTrip", cancellationToken);
        await using var first = OnDatabase(database);
        await using var second = OnDatabase(database);

        var payload = Protector(first).Protect("setup link");

        // The second instance starts only now, so it can know the key only from the database.
        Assert.Equal("setup link", Protector(second).Unprotect(payload));
        using var scope = second.Services.CreateScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<KeysDbContext>().DataProtectionKeys
            .ToListAsync(cancellationToken));
    }

    private WebApplicationFactory<Program> OnDatabase(string connectionString) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection([new("ConnectionStrings:Grow2Notes", connectionString)])));

    private static IDataProtector Protector(WebApplicationFactory<Program> app) =>
        app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(nameof(KeysDbContextTests));

    private static IReadOnlyList<MigrationOperation> Differences(DbContext db) =>
        db.GetService<IMigrationsModelDiffer>().GetDifferences(null, DesignTimeModel(db).GetRelationalModel());

    private static ITable KeyRingTable(DbContext db)
    {
        var table = DesignTimeModel(db).GetRelationalModel().FindTable("DataProtectionKeys", schema: null);
        Assert.NotNull(table);
        return table;
    }

    // Migrations read the design-time model; the runtime model leaves out what only they need.
    private static IModel DesignTimeModel(DbContext db) => db.GetService<IDesignTimeModel>().Model;

    private static IEnumerable<string> Describe(ITable table) => table.Columns.Select(column =>
    {
        var identity = column.PropertyMappings.Any(m =>
            m.Property.GetValueGenerationStrategy() == SqlServerValueGenerationStrategy.IdentityColumn);
        return $"{column.Name} {column.StoreType} {(column.IsNullable ? "NULL" : "NOT NULL")}"
               + (identity ? " IDENTITY" : "");
    });
}
