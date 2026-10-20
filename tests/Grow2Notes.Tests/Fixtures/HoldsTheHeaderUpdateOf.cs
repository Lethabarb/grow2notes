using System.Data.Common;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Holds back the first command, sent inside a transaction, that updates without reading the note whose ID it is
/// given, until the test lets it go: a conditional update of the note's header (design.md §5.8), before which the
/// request holds no lock: Submit's step 5, once it has found the note, the key and the working copy, or a discard's,
/// once it has found the draft and checked the caller. As <see cref="HoldsTheFirstSaveOfTheUser"/> holds a save, so
/// that a test's second request meanwhile runs to its end. Every other command passes.
/// </summary>
internal sealed class HoldsTheHeaderUpdateOf(Guid noteId) : DbCommandInterceptor
{
    private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int held;

    /// <summary>Completes once the command is held.</summary>
    public Task Reached => reached.Task;

    /// <summary>
    /// Whether <paramref name="command"/>, sent inside a transaction, updates the note of ID <paramref name="noteId"/>:
    /// of the commands that read no rows, only a conditional update of its header does.
    /// </summary>
    public static bool UpdatesTheHeaderOf(DbCommand command, Guid noteId) =>
        command.Transaction is not null
        && command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.Ordinal)
        && command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value is Guid id && id == noteId);

    /// <summary><paramref name="app"/>, with this interceptor on every context it makes.</summary>
    public WebApplicationFactory<Program> On(WebApplicationFactory<Program> app) =>
        app.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(this))));

    /// <summary>Lets the held command go on.</summary>
    public void Release() => released.TrySetResult();

    // ExecuteUpdate sends its UPDATE as a command that reads no rows, where SaveChanges reads back what it saved.
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (UpdatesTheHeaderOf(command, noteId) && Interlocked.Exchange(ref held, 1) == 0)
        {
            reached.SetResult();
            await released.Task.WaitAsync(cancellationToken);
        }

        return result;
    }
}
