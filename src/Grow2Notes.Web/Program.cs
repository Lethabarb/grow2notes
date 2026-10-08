using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IPNetwork = System.Net.IPNetwork;

var builder = WebApplication.CreateBuilder(args);

// Server telemetry goes to Application Insights only where it is configured, as in Azure (design.md §9.5). Its
// settings are read here, as the services are registered, for the same reasons as the Key Vault settings below.
builder.Services.AddTelemetryWhenConfigured(builder.Configuration);

builder.Services.AddHealthEndpoints();

// Database failures are logged in one place, with the exception type and SQL error number only, because SQL Server's
// error text can hold the values that failed (design.md §9.5).
builder.Services.AddExceptionHandler<DatabaseFailureHandler>();

// The body of every error response: RFC 9457 problem details (design.md §6.1), written by the exception handler and
// the status code pages below.
builder.Services.AddProblemDetails();

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

// App Service's front end ends TLS and calls the app over HTTP, adding the client's address, with its port, to
// X-Forwarded-For, and the scheme to X-Forwarded-Proto. Both are taken only from the private ranges that Microsoft's
// App Service guidance gives for the front end, as IPv4-mapped networks, since the app's dual-stack socket gives IPv4
// addresses in that form, and as plain IPv4 too, in case a socket gives them so; so audit events, and later the rate
// limits, key on the real client (design.md §9.9). The link-local range is trusted too, because Linux App Service's
// container sees the front-end hop from it (such as 169.254.130.1); it is not routable from outside the host, so no
// client can reach the app from it to send the headers itself.
// ForwardLimit keeps its default of 1: only the last X-Forwarded-For entry, the one the front end added, is used, and
// an address a client sent itself is ignored. Set here, not by ASPNETCORE_FORWARDEDHEADERS_ENABLED, which takes the
// headers from any address.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Add(IPNetwork.Parse("::ffff:10.0.0.0/104"));
    options.KnownIPNetworks.Add(IPNetwork.Parse("::ffff:172.16.0.0/108"));
    options.KnownIPNetworks.Add(IPNetwork.Parse("::ffff:192.168.0.0/112"));
    options.KnownIPNetworks.Add(IPNetwork.Parse("::ffff:169.254.0.0/112"));
    options.KnownIPNetworks.Add(IPNetwork.Parse("10.0.0.0/8"));
    options.KnownIPNetworks.Add(IPNetwork.Parse("172.16.0.0/12"));
    options.KnownIPNetworks.Add(IPNetwork.Parse("192.168.0.0/16"));
    options.KnownIPNetworks.Add(IPNetwork.Parse("169.254.0.0/16"));
});

var app = builder.Build();

// First, so that everything after it, the exception handler included, sees the client's address and scheme.
app.UseForwardedHeaders();

app.UseCacheHeaders();

// Both inside UseCacheHeaders, so its rules keep the final word on error responses. Status code pages give problem
// details to every error status that has no body, outside /api too. They come first, so that the bare status an
// exception handler sets, such as DatabaseFailureHandler's 500, gets them as well; for an exception that no handler
// takes, the exception handler writes them itself. Neither puts the exception's text in them, in any environment.
app.UseStatusCodePages();
app.UseExceptionHandler();

// The Vite build in wwwroot, served from the same origin as the API (design.md §7.2).
app.MapStaticAssets();

app.MapHealthEndpoints();

// Real API endpoints are more specific, so routing prefers them; any other /api path, and /api itself, is a 404 for
// every method, never the SPA page (design.md §6.1).
app.Map("/api/{**rest}", () => Results.NotFound());

// Client-side routes get index.html. The default pattern skips paths with a file extension, so a missing file such as
// /favicon.ico is a 404 rather than the page.
app.MapFallbackToFile("index.html");

app.Run();
