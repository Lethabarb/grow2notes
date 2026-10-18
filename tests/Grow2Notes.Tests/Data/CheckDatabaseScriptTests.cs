using System.Globalization;
using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// <c>infra/sql/check-database.sql</c>, which the operator runs after each deploy (ops/first-use-in-test.md), names
/// what the migrations make in its B7, B8, D3 and D5 rows: the tables, the migrations and the role's denies, B7, B8 and
/// D3 each with a count of its own. Nothing in CI runs the script, so these tests read it as text against the app's
/// model and its migrations, neither of which opens a connection. A migration added without the script's rows, or a
/// rebase that keeps both branches' lists but one branch's count, which git merges without a conflict where both
/// branches made the same change to it, then fails here rather than as a FAIL row on the operator's screen. Each
/// reader reports what it cannot read rather than pass over it.
/// </summary>
public sealed partial class CheckDatabaseScriptTests
{
    [Fact]
    public async Task B7_lists_every_table_of_the_model_and_the_history_table_and_expects_that_many()
    {
        var script = Script();
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var listed = Names(One(B7Tables(), script, "B7's list of tables"), QuotedName());

        Assert.Equal(
            db.Model.GetRelationalModel().Tables
                .Select(table => table.Name)
                .Append("__EFMigrationsHistory")
                .Order(StringComparer.Ordinal),
            listed.Order(StringComparer.Ordinal));
        Assert.Equal(
            (listed.Count, listed.Count),
            (Number(One(B7ExpectedText(), script, "B7's expected text")),
                Number(One(B7Count(), script, "B7's count"))));
    }

    [Fact]
    public async Task B8_names_every_migration_in_its_check_and_its_expected_text_and_expects_that_many()
    {
        var script = Script();
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var migrations = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database.GetMigrations();

        var checkedIds = Names(One(B8Ids(), script, "B8's list of migrations"), QuotedName());

        Assert.Equal(migrations, checkedIds);
        Assert.Equal(migrations, B8ExpectedId().Matches(script).Select(match => match.Groups["id"].Value));
        Assert.Equal(checkedIds.Count, Number(One(B8Count(), script, "B8's count")));
    }

    [Fact]
    public async Task D3_and_D5_check_every_deny_that_the_migrations_give_the_role_and_D3_expects_that_many()
    {
        var script = Script();
        var denies = await DeniesOfTheMigrationsAsync();
        var d3 = One(D3Expected(), script, "D3's expression for the expected denies");
        var d5 = One(D5Pass(), script, "D5's pass expression");

        Assert.Equal(
            denies.Select(deny => $"{deny.Permission} {deny.Table}").Order(StringComparer.Ordinal),
            Names(d3.Groups["deletes"].Value, ObjectId()).Select(table => $"DELETE {table}")
                .Concat(Names(d3.Groups["updates"].Value, ObjectId()).Select(table => $"UPDATE {table}"))
                .Order(StringComparer.Ordinal));

        // In the order in which D3 shows the denies that it finds: by table, then permission.
        Assert.Equal(
            denies.OrderBy(deny => deny.Table, StringComparer.Ordinal)
                .ThenBy(deny => deny.Permission, StringComparer.Ordinal)
                .Select(deny => $"{deny.Permission} {deny.Table}"),
            D3ExpectedDeny().Matches(script).Select(match => $"{match.Groups["permission"]} {match.Groups["table"]}"));
        var counts = One(D3Count(), script, "D3's counts");
        Assert.Equal(
            (denies.Count, denies.Count),
            (Number(counts.Groups["count"].Value), Number(counts.Groups["expected"].Value)));
        Assert.Equal(
            denies.Select(deny => $"{deny.Permission} {deny.Table}").Order(StringComparer.Ordinal),
            D5Refused().Matches(d5.Value)
                .Select(match => $"{match.Groups["permission"]} {match.Groups["table"]}")
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_script_whose_rows_cannot_be_read_fails()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            One(B7Count(), "CASE WHEN @b7Present >= 13 THEN 1 ELSE 0 END", "B7's count"));

        Assert.Equal("Read B7's count once in check-database.sql, where it was found 0 times.", error.Message);
    }

    private static string Script() =>
        File.ReadAllText(Path.Combine(Repository.Root(), "infra", "sql", "check-database.sql"))
            .ReplaceLineEndings("\n");

    // Every DENY in the migrations' Up operations, one for each permission that a statement names.
    private static async Task<List<(string Permission, string Table)>> DeniesOfTheMigrationsAsync()
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var migrations = db.GetService<IMigrationsAssembly>();

        return
        [
            .. migrations.Migrations
                .OrderBy(migration => migration.Key, StringComparer.Ordinal)
                .SelectMany(migration =>
                    migrations.CreateMigration(migration.Value, db.Database.ProviderName!).UpOperations)
                .OfType<SqlOperation>()
                .SelectMany(operation => Deny().Matches(operation.Sql))
                .SelectMany(deny => deny.Groups["permissions"].Value
                    .Split(", ")
                    .Select(permission => (permission, deny.Groups["table"].Value))),
        ];
    }

    private static Match One(Regex pattern, string script, string what)
    {
        var found = pattern.Matches(script);
        return found is [var match]
            ? match
            : throw new InvalidOperationException(
                $"Read {what} once in check-database.sql, where it was found {found.Count} times.");
    }

    private static List<string> Names(Match list, Regex name) => Names(list.Groups["list"].Value, name);

    private static List<string> Names(string list, Regex name) =>
        [.. name.Matches(list).Select(match => match.Groups["name"].Value)];

    private static int Number(Match match) => Number(match.Groups["number"].Value);

    private static int Number(string digits) => int.Parse(digits, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"FULL JOIN \(VALUES (?<list>.*?)\) AS e\(name\)", RegexOptions.Singleline)]
    private static partial Regex B7Tables();

    [GeneratedRegex(@"N'(?<number>[0-9]+) present, none missing'")]
    private static partial Regex B7ExpectedText();

    [GeneratedRegex(@"CASE WHEN @b7Present = (?<number>[0-9]+) AND @b7Missing IS NULL")]
    private static partial Regex B7Count();

    [GeneratedRegex(@"WHERE MigrationId IN \((?<list>.*?)\)\);", RegexOptions.Singleline)]
    private static partial Regex B8Ids();

    [GeneratedRegex(@"N'(?<id>[0-9]{14}_\w+) \(EF Core [0-9.]+\)")]
    private static partial Regex B8ExpectedId();

    [GeneratedRegex(@"CASE WHEN @b8Expected = (?<number>[0-9]+) THEN")]
    private static partial Regex B8Count();

    [GeneratedRegex(
        @"p\.permission_name = N'DELETE'\s+AND p\.major_id IN \((?<deletes>.*?)\)\)\s+"
        + @"OR \(p\.permission_name = N'UPDATE'\s+AND p\.major_id IN \((?<updates>.*?)\)\)\)\s+THEN",
        RegexOptions.Singleline)]
    private static partial Regex D3Expected();

    [GeneratedRegex(@"N'DENY (?<permission>[A-Z]+) on OBJECT_OR_COLUMN dbo\.(?<table>\w+)")]
    private static partial Regex D3ExpectedDeny();

    [GeneratedRegex(@"CASE WHEN @d3Count = (?<count>[0-9]+) AND @d3Expected = (?<expected>[0-9]+) THEN")]
    private static partial Regex D3Count();

    [GeneratedRegex(@"SET @d5Pass = CASE WHEN .*? THEN 1 ELSE 0 END;", RegexOptions.Singleline)]
    private static partial Regex D5Pass();

    [GeneratedRegex(@"HAS_PERMS_BY_NAME\(N'dbo\.(?<table>\w+)', N'OBJECT', N'(?<permission>[A-Z]+)'\) = 0")]
    private static partial Regex D5Refused();

    [GeneratedRegex(@"N'(?<name>\w+)'")]
    private static partial Regex QuotedName();

    [GeneratedRegex(@"OBJECT_ID\(N'dbo\.(?<name>\w+)', N'U'\)")]
    private static partial Regex ObjectId();

    [GeneratedRegex(
        @"DENY (?<permissions>[A-Z]+(?:, [A-Z]+)*) ON \[dbo\]\.\[(?<table>\w+)\] TO \[grow2notes_runtime\]")]
    private static partial Regex Deny();
}
