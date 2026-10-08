using System.Net.Http.Json;
using System.Security.Claims;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// The test-only sign-in's user is the principal that the app's claims factory builds from the user's row, as the
/// session cookie's stamp check rebuilds it on every request (design.md §8.4), so a test that calls as a seeded user
/// meets the claims that the app's own code reads; and <see cref="TestSignIn.RoleHeader"/> replaces the role claim and
/// no other.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class TestSignInTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    // Anyone may call it, so it shows the claims of a user whom the policies refuse too.
    private const string ClaimsPath = "/test-only/claims";

    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public TestSignInTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<IStartupFilter>(new ClaimsEndpoint())));
    }

    [Fact]
    public async Task A_request_that_names_a_user_holds_the_claims_factory_s_principal_for_them()
    {
        var fromTheFactory = await ClaimsFromTheFactoryAsync(seeded.A.WorkerId);
        using var client = app.CreateClient().SignInAs(seeded.A.WorkerId);

        var claims = await client.GetFromJsonAsync<string[]>(ClaimsPath, TestContext.Current.CancellationToken);

        Assert.Equal(fromTheFactory, claims);
    }

    [Theory]
    [InlineData("Support")]
    [InlineData(TestSignIn.NoRole)]
    public async Task The_role_header_replaces_the_role_claim_and_no_other(string role)
    {
        var roleClaimType = app.Services.GetRequiredService<IOptions<IdentityOptions>>().Value.ClaimsIdentity
            .RoleClaimType;
        var managersRole = Describe(new(roleClaimType, "Manager"));
        var fromTheFactory = await ClaimsFromTheFactoryAsync(seeded.A.ManagerId);
        // So that the test cannot pass by removing nothing.
        Assert.Contains(managersRole, fromTheFactory);
        List<string> expected = [.. fromTheFactory.Where(claim => claim != managersRole)];
        if (role != TestSignIn.NoRole)
        {
            expected.Add(Describe(new(roleClaimType, role)));
        }

        using var client = app.CreateClient().SignInAs(seeded.A.ManagerId);
        client.DefaultRequestHeaders.Add(TestSignIn.RoleHeader, role);

        var claims = await client.GetFromJsonAsync<string[]>(ClaimsPath, TestContext.Current.CancellationToken);

        // The replaced role claim comes last, so the order is not compared.
        Assert.NotNull(claims);
        Assert.Equal(expected.Order(StringComparer.Ordinal), claims.Order(StringComparer.Ordinal));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private async Task<List<string>> ClaimsFromTheFactoryAsync(Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByIdAsync(userId.ToString());
        Assert.NotNull(user);

        var principal = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>()
            .CreateAsync(user);
        return [.. principal.Claims.Select(Describe)];
    }

    private static string Describe(Claim claim) => $"{claim.Type}: {claim.Value}";

    /// <summary>
    /// Maps an endpoint that anyone may call, which lists the claims of the request's user. It is routed ahead of the
    /// app's own routing, which then leaves the endpoint already chosen, so it runs where the app's own endpoints do,
    /// after the app's authentication.
    /// </summary>
    private sealed class ClaimsEndpoint : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints => endpoints
                .MapGet(ClaimsPath, (ClaimsPrincipal user) => user.Claims.Select(Describe))
                .AllowAnonymous());
        };
    }
}
