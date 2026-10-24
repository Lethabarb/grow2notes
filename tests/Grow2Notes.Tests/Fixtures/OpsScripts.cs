using System.Data;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Runs a script of <c>ops/</c> as the operator's <c>sqlcmd</c> does, so that CI proves the file the operator runs
/// rather than a copy of it: it substitutes the <c>$(Name)</c> variables, starts the session with sqlcmd's
/// <c>QUOTED_IDENTIFIER OFF</c> so that a script missing its SET block fails here, and sends the script as one batch.
/// </summary>
internal static partial class OpsScripts
{
    // sqlcmd waits as long as the operator lets it; a script that takes longer than this is a script to look at.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    [GeneratedRegex(@"\$\((?<name>[A-Za-z_][A-Za-z0-9_]*)\)")]
    private static partial Regex VariableToken();

    [GeneratedRegex(@"^[A-Za-z0-9-]*\z")]
    private static partial Regex AllowedValue();

    /// <summary>
    /// Runs <paramref name="scriptPath"/>, relative to the repository's root, on the database at
    /// <paramref name="connectionString"/> with <paramref name="variables"/>, and returns each result set, in order.
    /// A <c>THROW</c> surfaces as a <see cref="SqlException"/>, whose <c>Number</c> is 50000 for a script's refusal.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A variable that the script uses was not given, one that was given is not used, or a value has a character other
    /// than a letter, a digit or a hyphen. An empty value is allowed.
    /// </exception>
    public static async Task<IReadOnlyList<DataTable>> RunAsync(
        string connectionString,
        string scriptPath,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(Repository.Root(), scriptPath);
        var script = Substitute(await File.ReadAllTextAsync(path, cancellationToken), variables);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using (var start = connection.CreateCommand())
        {
            start.CommandText = "SET QUOTED_IDENTIFIER OFF";
            await start.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = script;
        command.CommandTimeout = (int)Timeout.TotalSeconds;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<DataTable>();
        do
        {
            // A statement that returns no rows, such as DBCC CHECKDB when the database is clean, has no columns.
            if (reader.FieldCount == 0)
            {
                continue;
            }

            var table = new DataTable();
            for (var column = 0; column < reader.FieldCount; column++)
            {
                table.Columns.Add(reader.GetName(column), reader.GetFieldType(column));
            }

            while (await reader.ReadAsync(cancellationToken))
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                table.Rows.Add(values);
            }

            results.Add(table);
        }
        while (await reader.NextResultAsync(cancellationToken));
        return results;
    }

    /// <summary>
    /// A migrated database of the test's own on <paramref name="sql"/>, with nothing seeded, and the app on it, so
    /// that the test can add its rows through the app's services. <paramref name="name"/> is unique in the collection.
    /// </summary>
    public static async Task<OpsDatabase> OnDatabaseOfItsOwnAsync(
        SqlServerFixture sql, string name, CancellationToken cancellationToken = default)
    {
        var connectionString = await sql.CreateDatabaseAsync(name, cancellationToken);
        var shared = new Grow2NotesFactory(sql);
        var app = shared.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection([new("ConnectionStrings:Grow2Notes", connectionString)])));
        return new(shared, app, connectionString);
    }

    /// <summary>
    /// <paramref name="script"/> with every <c>$(Name)</c> replaced by its value, as sqlcmd replaces it, comments
    /// included, so a script that mentions the token in a comment fails here as it would for the operator.
    /// </summary>
    internal static string Substitute(string script, IReadOnlyDictionary<string, string>? variables)
    {
        var given = new Dictionary<string, string>(variables ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
        var used = VariableToken().Matches(script).Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = used.Except(given.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count > 0)
        {
            throw new ArgumentException($"The script uses {string.Join(", ", missing)}, which was not given.");
        }

        var unused = given.Keys.Except(used, StringComparer.OrdinalIgnoreCase).ToList();
        if (unused.Count > 0)
        {
            throw new ArgumentException($"{string.Join(", ", unused)} was given, but the script does not use it.");
        }

        foreach (var (name, value) in given)
        {
            if (!AllowedValue().IsMatch(value))
            {
                throw new ArgumentException($"The value of {name} has a character other than a letter, a digit or -.");
            }
        }

        return VariableToken().Replace(script, match => given[match.Groups["name"].Value]);
    }
}

/// <summary>
/// The database that <see cref="OpsScripts.OnDatabaseOfItsOwnAsync"/> made, with the app's services on it and the
/// <c>sa</c> connection string that <see cref="OpsScripts.RunAsync"/> takes.
/// </summary>
internal sealed class OpsDatabase(
    Grow2NotesFactory shared, WebApplicationFactory<Program> app, string connectionString) : IAsyncDisposable
{
    public IServiceProvider Services => app.Services;

    public string ConnectionString => connectionString;

    public async ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
        await shared.DisposeAsync();
    }
}
