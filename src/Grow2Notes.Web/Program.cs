using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthEndpoints();

// The connection string is read when a context is configured, not here: configuration that WebApplicationFactory adds
// arrives only when the host is built, so reading it here would miss the test database.
//
// Both contexts retry transient failures, such as Azure SQL's brief failovers, with the provider's default limits
// (design.md §5.9). The key ring needs it as much as the app's data: Data Protection loads the keys at startup and
// whenever the ring refreshes, and it saves a new key with a plain SaveChanges, which the strategy retries by itself.
static void UseGrow2NotesDatabase(IServiceProvider services, DbContextOptionsBuilder options) =>
    options.UseSqlServer(
        services.GetRequiredService<IConfiguration>().GetConnectionString("Grow2Notes"),
        sql => sql.EnableRetryOnFailure());

builder.Services.AddDbContext<Grow2NotesDbContext>(UseGrow2NotesDatabase);
builder.Services.AddDbContext<KeysDbContext>(UseGrow2NotesDatabase);

// The key ring that protects the sign-in cookie and setup links lives in the database, so a restart or a deploy does
// not sign everyone out (design.md §9.3). The application name replaces the default, the content root path, which
// follows the working directory: setup links that the operator commands print from the SSH console (§7.4) must open in
// the app.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<KeysDbContext>()
    .SetApplicationName("Grow2Notes");

// Grow2NotesDbContext reads the schema version from these options when it builds its model; Version3 adds the passkey
// table (design.md §8.4). The store is user-only, because the context has no role tables.
// Throwaway: MaxLengthForKeys resizes the AspNetUserLogins and AspNetUserTokens key columns from 128 to 64, a model
// change with no migration. The test fixture builds its model from the schema version alone, so only the
// pending-model-changes check sees it.
builder.Services.AddIdentityCore<ApplicationUser>(o =>
    {
        o.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        o.Stores.MaxLengthForKeys = 64;
    })
    .AddEntityFrameworkStores<Grow2NotesDbContext>();

// Code asks TimeProvider for the time, never DateTime.Now, so tests can replace the clock (design.md §7.5).
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<MelbourneClock>();

var app = builder.Build();

app.UseCacheHeaders();

// The Vite build in wwwroot, served from the same origin as the API (design.md §7.2).
app.MapStaticAssets();

app.MapHealthEndpoints();

// Real API endpoints are more specific, so routing prefers them; any other /api path, and /api itself, is a 404 for
// every method, never the SPA page (design.md §6.1).
app.Map("/api/{**rest}", () => Results.NotFound());

// Client-side routes get index.html. The default pattern skips paths with a file extension, so a missing file such as
// /favicon.ico is a plain 404 rather than the page.
app.MapFallbackToFile("index.html");

app.Run();
