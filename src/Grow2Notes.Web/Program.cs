using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Server telemetry goes to Application Insights only where it is configured, as in Azure (design.md §9.5). Its
// settings are read here, as the services are registered, for the same reasons as the Key Vault settings below.
builder.Services.AddTelemetryWhenConfigured(builder.Configuration);

builder.Services.AddHealthEndpoints();

// Database failures are logged in one place, with the exception type and SQL error number only, because SQL Server's
// error text can hold the values that failed (design.md §9.5).
builder.Services.AddExceptionHandler<DatabaseFailureHandler>();

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
//
// In Azure the Key Vault key also wraps each key. Unlike the connection string, its settings are read here, as the
// services are registered: they decide which services Data Protection gets, and a missing client ID must stop the app
// starting. App Service passes them as environment variables, which are loaded before this line runs.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<KeysDbContext>()
    .SetApplicationName("Grow2Notes")
    .ProtectKeysWithKeyVaultWhenConfigured(builder.Configuration);

// Grow2NotesDbContext reads the schema version from these options when it builds its model; Version3 adds the passkey
// table (design.md §8.4). The store is user-only, because the context has no role tables.
builder.Services.AddIdentityCore<ApplicationUser>(o => o.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
    .AddEntityFrameworkStores<Grow2NotesDbContext>();

// The tenant of each request or operator command: the signed-in user's organisation, or the one that sign-in, setup
// and the commands set from what they have loaded (design.md §5.9). It reads the user from the request's HttpContext.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();

// Code asks TimeProvider for the time, never DateTime.Now, so tests can replace the clock (design.md §7.5).
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<MelbourneClock>();

// One per scope, so each event is saved through the scope's context and joins the transaction open there, committing
// or rolling back with the change it records (design.md §5.9).
builder.Services.AddScoped<IAuditWriter, AuditWriter>();

var app = builder.Build();

app.UseCacheHeaders();

// Inside UseCacheHeaders, so its rules keep the final word on error responses. The middleware will not start without
// a path, a delegate or problem details for the exceptions no handler takes. This delegate adds nothing, so they keep
// the empty 500 they got before; design.md §6.1's problem+json errors are not built yet.
app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = static _ => Task.CompletedTask });

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
