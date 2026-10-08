using System.Net;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Signing in, in tests, with the real session cookie, for the tests of the session itself (design.md §8.4, §8.5). No
/// sign-in endpoint exists until S00.04.02, and the seeded users have no passkey or password, so a test maps a
/// test-only endpoint (<see cref="MapCookieSignIn"/>) that signs a user in with the app's
/// <c>SignInManager.SignInAsync(user, isPersistent: false)</c>: the call that ends setup (§8.1 step 5), which passes
/// through the sign-in manager's <c>SignInWithClaimsAsync</c>, where every sign-in ends. So the client gets the cookie
/// that a real sign-in issues, <c>auth_time</c> included, and its later requests meet the app's own session checks.
/// </summary>
internal static class CookieSignIn
{
    // Outside the /api group, whose filter would ask a signed-out caller for an antiforgery token first.
    private const string Path = "/test-only/sign-in";

    /// <summary>Maps the sign-in endpoint in the app that <paramref name="builder"/> builds.</summary>
    public static IWebHostBuilder MapCookieSignIn(this IWebHostBuilder builder) =>
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter>(new SignInEndpoint()));

    /// <summary>
    /// Signs <paramref name="client"/> in as the user whose ID is <paramref name="userId"/>, and returns the cookie
    /// that the sign-in's one <c>Set-Cookie</c> sets. The client is one from
    /// <see cref="AntiforgeryTokens.CreateHttpsClient"/>: the cookie is <c>Secure</c>, so it is sent over HTTPS only,
    /// and the client keeps it and sends it with every later request, which goes to the cookie as long as it has
    /// neither of <see cref="TestSignIn"/>'s headers.
    /// </summary>
    /// <remarks>
    /// <c>SignInAsync</c> does not ask <c>CanSignInAsync</c>, so sign in Active users only, as setup does.
    /// </remarks>
    public static async Task<SetCookieHeaderValue> SignInWithCookieAsync(this HttpClient client, Guid userId)
    {
        using var response = await client.PostAsync(
            $"{Path}/{userId}", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return Assert.Single(response.SetCookies());
    }

    /// <summary>The cookies that <paramref name="response"/>'s <c>Set-Cookie</c> headers set, in their order.</summary>
    public static IList<SetCookieHeaderValue> SetCookies(this HttpResponseMessage response) =>
        response.Headers.TryGetValues(HeaderNames.SetCookie, out var values)
            ? SetCookieHeaderValue.ParseList([.. values])
            : [];

    /// <summary>
    /// Maps <c>POST</c> at <see cref="Path"/> and a user's ID, which anyone may call, since its caller is signed out
    /// until it answers. It is routed ahead of the app's own routing, which then leaves the endpoint already chosen, so
    /// it runs where the app's own endpoints do, after the app's authentication.
    /// </summary>
    private sealed class SignInEndpoint : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints => endpoints.MapPost($"{Path}/{{userId:guid}}", SignInAsync).AllowAnonymous());
        };

        private static async Task<Results<NoContent, NotFound>> SignInAsync(
            Guid userId, SignInManager<ApplicationUser> signInManager)
        {
            if (await signInManager.UserManager.FindByIdAsync(userId.ToString()) is not { } user)
            {
                return TypedResults.NotFound();
            }

            await signInManager.SignInAsync(user, isPersistent: false);
            return TypedResults.NoContent();
        }
    }
}
