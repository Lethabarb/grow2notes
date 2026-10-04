using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The design.md §5.1 conventions, checked on every entity in the app's model, so each entity added later is checked
/// too. <see cref="ModelConventionsOnNewEntitiesTests"/> covers the rows that no entity in the model uses yet.
/// </summary>
[Collection<SqlServerCollection>]
public sealed partial class ModelConventionsTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    [Fact]
    public void Tables_are_named_after_their_entity_and_the_Identity_tables_keep_their_AspNet_names()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var identityTables = new Dictionary<Type, string>
        {
            [typeof(ApplicationUser)] = "AspNetUsers",
            [typeof(IdentityUserClaim<Guid>)] = "AspNetUserClaims",
            [typeof(IdentityUserLogin<Guid>)] = "AspNetUserLogins",
            [typeof(IdentityUserToken<Guid>)] = "AspNetUserTokens",
            [typeof(IdentityUserPasskey<Guid>)] = "AspNetUserPasskeys",
        };

        // An owned type, such as Identity's passkey data, is stored in its owner's table.
        Assert.All(db.Model.GetEntityTypes().Where(e => !e.IsOwned()), entity =>
            Assert.Equal(identityTables.GetValueOrDefault(entity.ClrType, entity.ClrType.Name), entity.GetTableName()));
    }

    [Fact]
    public void Guid_keys_get_sequential_GUIDs_that_EF_Core_generates_on_the_client()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var generators = db.GetService<IValueGeneratorSelector>();

        var keys = db.Model.GetEntityTypes()
            .Select(e => e.FindPrimaryKey()?.Properties)
            .Where(key => key is [{ ClrType: var type }] && type == typeof(Guid))
            .Select(key => key![0])
            .ToList();

        Assert.NotEmpty(keys);
        Assert.All(keys, key =>
        {
            Assert.Equal(ValueGenerated.OnAdd, key.ValueGenerated);
            Assert.Null(key.GetDefaultValueSql());
            Assert.True(generators.TrySelect(key, key.DeclaringType, out var generator));
            Assert.IsType<SequentialGuidValueGenerator>(generator);
        });
    }

    [Fact]
    public void DateTime_columns_are_named_Utc_stored_as_datetime2_3_and_read_back_as_UTC()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var properties = db.Model.GetEntityTypes().SelectMany(e => e.GetProperties()).ToList();
        var dateTimes = properties.Where(IsDateTime).ToList();

        Assert.Contains(dateTimes, p => p.ClrType == typeof(DateTime));
        Assert.Contains(dateTimes, p => p.ClrType == typeof(DateTime?));
        Assert.All(properties, p => Assert.Equal(IsDateTime(p), p.Name.EndsWith("Utc", StringComparison.Ordinal)));
        Assert.All(dateTimes, p =>
        {
            Assert.Equal("datetime2(3)", p.GetColumnType());

            var stored = new DateTime(2026, 10, 4, 1, 30, 15, 250, DateTimeKind.Unspecified);
            var read = Assert.IsType<DateTime>(p.GetTypeMapping().Converter?.ConvertFromProvider(stored));
            Assert.Equal(DateTimeKind.Utc, read.Kind);
            Assert.Equal(stored.Ticks, read.Ticks);
        });

        static bool IsDateTime(IProperty p) => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?);
    }

    [Fact]
    public async Task Enum_columns_are_tinyint_from_byte_enums_whose_members_all_have_explicit_values()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var enumProperties = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType).IsEnum)
            .ToList();
        var source = Comment().Replace(await ReadWebProjectSourceAsync(), "");

        Assert.NotEmpty(enumProperties);
        Assert.All(enumProperties, p =>
        {
            var type = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
            Assert.Equal(typeof(byte), Enum.GetUnderlyingType(type));
            Assert.Equal("tinyint", p.GetColumnType());

            // A compiled enum cannot show whether a value was written or implied by the member's position, and an
            // implied value changes when a member is inserted above it, so this reads the declaration.
            var members = Assert.Single(EnumDeclaration().Matches(source), m => m.Groups["name"].Value == type.Name)
                .Groups["members"].Value;
            Assert.All(Enum.GetNames(type), name => Assert.Matches($@"\b{name}\s*=", members));
        });
    }

    [Fact]
    public void Every_foreign_key_is_ON_DELETE_NO_ACTION()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        // An ownership is part of its owner's row, with no foreign key in the database.
        var foreignKeys = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .Where(fk => !fk.IsOwnership)
            .ToList();

        Assert.NotEmpty(foreignKeys);
        Assert.All(foreignKeys, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));

        // SQL Server has no RESTRICT, so the script EF Core gives the database says NO ACTION for each foreign key.
        var script = db.Database.GenerateCreateScript();
        Assert.Equal(foreignKeys.Count, Regex.Count(script, "FOREIGN KEY"));
        Assert.Equal(foreignKeys.Count, Regex.Count(script, "ON DELETE NO ACTION"));
    }

    private async Task<string> ReadWebProjectSourceAsync()
    {
        var webProject = factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
        var files = Directory.EnumerateFiles(webProject, "*.cs", SearchOption.AllDirectories)
            .Where(f => Path.GetRelativePath(webProject, f).Split(Path.DirectorySeparatorChar)[0]
                is not ("bin" or "obj"));

        var sources = await Task.WhenAll(files.Select(f =>
            File.ReadAllTextAsync(f, TestContext.Current.CancellationToken)));
        return string.Join('\n', sources);
    }

    [GeneratedRegex(@"\benum\s+(?<name>\w+)[^{]*\{(?<members>[^}]*)\}")]
    private static partial Regex EnumDeclaration();

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();
}
