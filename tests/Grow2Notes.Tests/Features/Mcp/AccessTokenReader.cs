using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// Reads an access token as the app does when it validates one: through OpenIddict's own <c>ValidateTokenContext</c>,
/// dispatched in a transaction of its own, so the token's format, its lifetime and its claims are what OpenIddict
/// makes of it, not what a test guesses of Data Protection's payload.
/// </summary>
internal static class AccessTokenReader
{
    /// <summary>The principal of <paramref name="token"/>, which the test asserts is valid.</summary>
    public static async Task<ClaimsPrincipal> ReadAsync(this WebApplicationFactory<Program> app, string token)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IOpenIddictServerFactory>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictServerDispatcher>();

        var context = new OpenIddictServerEvents.ValidateTokenContext(await factory.CreateTransactionAsync())
        {
            Token = token,
            TokenTypeHint = TokenTypeHints.AccessToken,
        };
        context.ValidTokenTypes.Add(TokenTypeHints.AccessToken);
        await dispatcher.DispatchAsync(context);

        Assert.False(context.IsRejected, $"{context.Error}: {context.ErrorDescription}");
        return Assert.IsType<ClaimsPrincipal>(context.Principal);
    }
}
