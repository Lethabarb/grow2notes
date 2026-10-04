using Grow2Notes.Web.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthEndpoints();

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
