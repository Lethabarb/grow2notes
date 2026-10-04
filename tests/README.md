# tests

Automated tests for Grow2Notes (design.md §7.3, §10.4).

- `Grow2Notes.Tests/`: xUnit v3 unit tests and API integration tests. The integration tests host the app with
  `WebApplicationFactory` against a real SQL Server in Testcontainers, so Docker must be running.
- `e2e/`: Playwright smoke tests with axe accessibility checks, run against the published app.

## Running the .NET tests

The integration tests serve the real SPA build, so build the SPA first, and again after changing it:

```shell
cd src/grow2notes-spa
npm ci
npm run build
cd ../..
dotnet test Grow2Notes.slnx
```

If `src/Grow2Notes.Web/wwwroot` has no build, the test project's build stops with an error saying so.

## Writing integration tests

- Put the test class in `[Collection<SqlServerCollection>]` and take `Grow2NotesFactory` as a class fixture. Every class
  in that collection shares one SQL Server container for the run, and xUnit runs their tests one at a time.
- A test that stops its database (`SqlServerFixture.StopAsync`) declares a collection of its own with
  `ICollectionFixture<SqlServerFixture>`, so it gets its own container and cannot break the shared one. It starts the
  app (`factory.CreateClient()`) before stopping the database, because a stopped container has no port to build the
  connection string from. `Grow2Notes.Tests/Platform/HealthEndpointsWithDatabaseStoppedTests.cs` is the example.
- The factory runs the app in the `Testing` environment, so `appsettings.Development.json` is not loaded, and its
  connection string overrides any on the machine.
- A run filtered to unit tests starts no container.
