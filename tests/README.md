# tests

Automated tests for Grow2Notes (design.md §7.3, §10.4).

- `Grow2Notes.Tests/`: xUnit v3 unit tests and API integration tests. The integration tests host the app with
  `WebApplicationFactory` against a real SQL Server in Testcontainers, so Docker must be running.
- `e2e/`: Playwright smoke tests with axe accessibility checks, run against the published app.

The SPA's unit tests are not here: they sit next to the code they test, as `*.test.ts` or `*.test.tsx` files under
`src/grow2notes-spa/src`, and run in Vitest with Testing Library.

## Running the SPA checks

Each is a separate step of CI's SPA checks (design.md §10.4 step 2). In `src/grow2notes-spa`, after `npm ci`:

```shell
npm run typecheck           # tsc over the app, the tests, the config files and scripts/
npm run lint                # ESLint; a warning fails it too
npm test                    # Vitest, once
npm run build               # vite build into src/Grow2Notes.Web/wwwroot
npm run check:no-analytics  # no Application Insights JavaScript SDK or third-party analytics (design.md §9.5)
```

`npm run build` does not type-check, so a type error shows up only in `npm run typecheck`.

`npm run check:no-analytics` reads every package in `package-lock.json` and every file of the build, so it runs after
`npm run build`; its denylist is at the top of `scripts/check-no-analytics.ts`.

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

## Running the end-to-end tests

CI runs them against the published app, on a SQL Server container that the migrations bundle has migrated (the
end-to-end steps of `.github/workflows/ci.yml`). To do the same on a machine, build the SPA as above, then from the
repository root, with Docker running:

```shell
docker run -d --name grow2notes-e2e-sql -p 1433:1433 -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=Grow2Notes-CI-only-1 \
  mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04
# Once SQL Server has started, about 15 seconds later:
export ConnectionStrings__Grow2Notes="Server=localhost,1433;Database=Grow2Notes;User ID=sa;Password=Grow2Notes-CI-only-1;TrustServerCertificate=True"
dotnet tool restore
dotnet ef database update --project src/Grow2Notes.Web --context Grow2NotesDbContext \
  --connection "$ConnectionStrings__Grow2Notes"
dotnet publish src/Grow2Notes.Web --configuration Release --output artifacts/app
cd artifacts/app
ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://localhost:5000 dotnet Grow2Notes.Web.dll
```

Then, in a second terminal:

```shell
cd tests/e2e
npm ci
npx playwright install chromium
npm run typecheck
npm test
```

The tests open `http://localhost:5000` unless `E2E_BASE_URL` names another address. After a failure,
`npx playwright show-report` opens the HTML report, which holds a trace of each failed test. Remove the database with
`docker rm -f grow2notes-e2e-sql` when done.

## Writing integration tests

- Put the test class in `[Collection<SqlServerCollection>]` and take `Grow2NotesFactory` as a class fixture. Every class
  in that collection shares one SQL Server container for the run, and xUnit runs their tests one at a time.
- `SqlServerFixture` applies the app's migrations to the container's `Grow2Notes` database before the first test, as a
  deploy runs them before the new code starts; the app never migrates itself (design.md §10.6). Every test in the
  collection shares that database, so a test finds its rows by their keys and never assumes a table is empty. A test
  that must leave nothing behind runs in a transaction that it rolls back; one that must know every row in a table
  takes a migrated database of its own from `SqlServerFixture.CreateDatabaseAsync`. A test-only context gets a
  database of its own from `SqlServerFixture.ConnectionStringFor` and creates it from its model with `EnsureCreated`;
  `Grow2Notes.Tests/Data/TenantIsolationOnNewEntitiesTests.cs` is the example.
- The app under test connects as `sa`, to which the denies of `grow2notes_runtime` do not apply. A test of what the
  database refuses the app runs its statements as a member of that role, as the app's identity is in Azure.
  `Grow2Notes.Tests/Data/RuntimeRoleTests.cs` is the example.
- A test that stops its database (`SqlServerFixture.StopAsync`) declares a collection of its own with
  `ICollectionFixture<SqlServerFixture>`, so it gets its own container and cannot break the shared one. It starts the
  app (`factory.CreateClient()`) before stopping the database, because a stopped container has no port to build the
  connection string from. `Grow2Notes.Tests/Platform/HealthEndpointsWithDatabaseStoppedTests.cs` is the example.
- The factory runs the app in the `Testing` environment, so `appsettings.Development.json` is not loaded, and its
  connection string overrides any on the machine.
- A test that only reads the app's services or settings, and sends no request that needs the database, can host the
  app without one in `AppWithoutDatabase`, so it runs without Docker. `Grow2Notes.Tests/Platform/LoggingTests.cs` is
  the example, and `Grow2Notes.Tests/Data/ModelTenancyTests.cs` reads the app's model that way.
- A run filtered to unit tests starts no container.
