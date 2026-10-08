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
  deploy runs them before the new code starts; the app never migrates itself (design.md §10.6). It then seeds the two
  organisations described below. Every test in the collection shares that database, so a test finds its rows by their
  keys and never assumes a table is empty. A test that must leave nothing behind runs in a transaction that it rolls
  back; one that must know every row in a table takes a migrated database of its own, with nothing seeded, from
  `SqlServerFixture.CreateDatabaseAsync`. A test-only context gets a database of its own from
  `SqlServerFixture.ConnectionStringFor` and creates it from its model with `EnsureCreated`;
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
  the example, and `Grow2Notes.Tests/Data/ModelTenancyTests.cs` reads the app's model that way. It hosts the app
  without the test-only sign-in (below), as the app is deployed, so `Grow2Notes.Tests/SignedOutTests.cs` uses it to
  show what a signed-out caller gets.
- A test-only endpoint that a test maps is denied by default, like any endpoint of the app's (design.md §2), so mark
  it `AllowAnonymous` unless the test is about who may call it. In `AppWithoutDatabase`, where the session cookie is
  the default scheme, as in the deployed app, a signed-out request to one that is not gets the cookie's `401`, as
  problem details (`Grow2Notes.Tests/SignedOutTests.cs`).
- A test that calls `GET /api/auth/antiforgery` or an endpoint in the `/api` group calls `https://localhost`, with a
  client from `CreateHttpsClient()` (`Grow2Notes.Tests/Fixtures/AntiforgeryTokens.cs`): the antiforgery cookie is
  `Secure` always, so antiforgery throws on a request that is not HTTPS, and the app answers `500`. A request that
  changes something sends the token that `FetchTokenAsync()` fetched with the same client, which keeps the cookie, in
  its `X-XSRF-TOKEN` header, as the SPA does (design.md §9.8), and sends any body as `application/json`, as
  `PostAsJsonAsync` and `JsonContent` do: the group answers `415` to a request with any other `Content-Type`, or with
  a body and none. A token is issued to the user a request is signed in as, so fetch it after signing the client in.
  In `AppWithoutDatabase`, Data Protection, which protects the tokens, has no key ring until `KeepKeysInMemory()` gives
  it one. A test-only endpoint mapped in the group, as `TestOnlyApiEndpoint` maps one, gets the group's filter; one
  mapped outside it, as most tests map theirs, does not.
- A run filtered to unit tests starts no container.

### The seeded organisations and the tenant

After migrating the shared database, `SqlServerFixture` adds two made-up organisations (A35) for the two-organisation
tests (design.md §5.9 item 7), each with an Active manager and an Active worker with an `example.org` address. A test
takes `SqlServerFixture` in its constructor, beside `Grow2NotesFactory`, and finds their IDs in `sqlServer.Seeded`:
`Seeded.A` and `Seeded.B` each hold the `OrganisationId`, `ManagerId` and `WorkerId`. A two-organisation test acts in A
and checks that nothing of B's reaches it. `Grow2Notes.Tests/Fixtures/SeededOrganisationsTests.cs` shows what is
seeded.

- Other tests rely on the seeded IDs, and on each seeded user's email address, role, Active status, security stamp
  and lockout (Identity's count of failed sign-ins and its lockout end), so no test changes them. An endpoint call
  commits, and may change other columns of a seeded row, such as Identity's `ConcurrencyStamp`. A test may add rows
  to a seeded organisation, users included, so no test assumes the seeded users are its only ones. A test that
  deactivates or resets a user, changes a user's role or email, or fails a sign-in on purpose does it to a user it
  added itself, and one that invites through the API invites an address of its own; or it works in a migrated
  database of its own from `SqlServerFixture.CreateDatabaseAsync`.
- The users are created through `UserManager`, as the app creates accounts, and are as setup leaves them except that
  they have no passkey or password, so a test calls as them with the test-only sign-in.

Tenant-owned rows are read and written under a tenant (design.md §5.9). In a request, the app takes it from the
signed-in user's session. A test that uses the app's context outside a request sets it as sign-in and the operator
commands do: it takes `ITenantContext` from the same scope as the context and opens a `Use` block.

```csharp
using var scope = factory.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();

using (tenant.Use(sqlServer.Seeded.A.OrganisationId))
{
    // Queries return only A's rows; a save stamps A's ID on added rows and throws for another organisation's.
}
```

With no `Use` block open, a query or save of a tenant-owned row throws. Open the block in the test method itself,
around its awaits: a tenant set inside an awaited helper method does not flow back to its caller. `Organisation` and
`AspNetUsers` need no tenant: the first is the tenant, and the second is not filtered, so a query of users names the
organisation itself, as `SeededOrganisationsTests` does.

### Calling as a seeded user

`Grow2NotesFactory` makes a test-only authentication scheme the app's default
(`Grow2Notes.Tests/Fixtures/TestSignIn.cs`); the app itself has no such scheme. A request that names a user's ID in its
`X-Test-User` header is signed in as that user, with the user ID, `org_id` and role claims that design.md §8.4's claims
factory adds, read from the user's row: the user's ID, `org_id`, which makes the user's organisation the request's
tenant, and the role by its `UserRole` name. A request without the header is signed out. `SignInAs` adds the header to
every request a client sends:

```csharp
using var client = factory.CreateClient().SignInAs(sqlServer.Seeded.A.WorkerId);
```

`SignInAs(caller, sqlServer.Seeded)` signs in as one of the endpoint matrix's callers (below), or, for
`Caller.SignedOut`, adds nothing.

A request that the fallback policy or the `Manager` policy refuses then answers `401` signed out and `403` signed in,
as problem details. To test a role that `UserRole` does not have, such as Release 2's Support, a request also sends
the `X-Test-Role` header, whose value replaces the role claim's; the value `TestSignIn.NoRole` leaves the user with no
role claim (an empty value would not: the test server drops a header whose value is empty).
`Grow2Notes.Tests/Platform/PoliciesOnSqlServerTests.cs` is the example.

### The endpoint matrix

`Grow2Notes.Tests/EndpointMatrixTests.cs` takes every endpoint from the app's `EndpointDataSource` and calls it as each
`Caller`: signed out, A's worker, A's manager and B's manager (design.md §2, §9.2). Each must answer with the status in
the endpoint's row in `Grow2Notes.Tests/EndpointMatrix.cs`; the matrix checks the status only. An endpoint with no row
fails the run, and so does a row with no endpoint, so a story that maps, changes or removes an endpoint changes its row
in the same change. The app is hosted there with no test-only endpoints, as in production, so an endpoint that a test
maps for itself needs no row.

- A row finds its endpoint by the methods it is mapped with and its route pattern as mapped, the `/api` group's prefix
  included: `["GET"]` for `MapGet`, and `AnyMethod` for an endpoint mapped with `Map` or `MapFallback`, which names no
  method and is called with `GET`, `POST`, `PUT` and `DELETE`. One row stands for every file of the SPA build
  (`StaticAssets`), whose names change with each build.
- The four statuses follow design.md §2 and §9.1: `401` signed out at an endpoint that is not `AllowAnonymous`, `403`
  for A's worker at one with the `Manager` policy, and, from the first endpoint that loads one of an organisation's
  records, `404` for B's manager at A's (§9.2).
- By default the matrix calls an endpoint at its route pattern. A row whose route has parameters builds its own
  request, such as `At("/api/no-such-path")`, and its builder is asked again for each call. A call commits, so a
  row whose calls change something builds each call's request after adding the record that call acts on, in
  organisation A, as the seeded-rows rule above says; the first such row gives `EndpointCall` the app's services and
  the seeded organisations to do it with.
- The matrix calls `https://localhost`, as the SPA's antiforgery token needs (above), and fetches each caller's own
  token first. A call that changes something (`EndpointCall.ChangesState`: a method other than `GET`, `HEAD`, `OPTIONS`
  or `TRACE`, at an endpoint that names its methods) sends that token, and the matrix makes it twice more: without one,
  expecting `400` from the `/api` group's filter, and with it but with a form-encoded body in place of the row's,
  expecting `415` (§9.8 item 3; at an endpoint that reads JSON, binding answers the form body's `415` first, the same
  status). Of the statuses a row gives, only authorization's `401` and `403` come before the filter (binding and
  validation run first too, but a row's requests are valid), so at an endpoint that is not `AllowAnonymous`, the
  matrix expects the row's `401` or `403` to both calls too. At one that is, such as `POST /api/auth/login`, whose
  `401` to bad credentials is its own (design.md §6.2), it expects `400` and `415`. So a row's statuses are those of a
  call with the token, and an endpoint that changes something outside the group fails the run, even one that answers
  `401` to every caller. A `403` that the handler of an endpoint that is not `AllowAnonymous` gives, such as
  `note.not_editable` (§6.3), comes after the filter, yet the matrix expects it to both calls too; the first such row
  will need to say what they get. An endpoint that names no method, such as a catch-all, is called with `POST`, `PUT`
  and `DELETE` too, but changes nothing, and gets no token.
- `EndpointMatrix.CallsTo` lists every call the matrix makes, and `EndpointCall.RequestAsync` builds the request as
  the endpoint's row does, so a test of every endpoint, such as `PoliciesOnSqlServerTests`' role test, uses them. An
  endpoint with no row, such as one the test maps for itself, gets a request to its route pattern.
