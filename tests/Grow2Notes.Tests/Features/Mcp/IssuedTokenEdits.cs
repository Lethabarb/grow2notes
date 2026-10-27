using Grow2Notes.Web.Features.Mcp.OAuth;
using Grow2Notes.Web.Platform;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;
using static OpenIddict.Server.OpenIddictServerHandlers;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>What <see cref="IssuedTokenEdits"/> changes in an access token before it is signed.</summary>
public enum TokenEdit
{
    /// <summary>The token is for another resource than <c>/mcp</c>.</summary>
    AnotherAudience,

    /// <summary>The token holds no hash of the person's security stamp.</summary>
    NoStampHash,

    /// <summary>The token holds the hash of another stamp than the person's.</summary>
    AnotherStampHash,

    /// <summary>The token names an organisation that the person is not in.</summary>
    AnotherOrganisation,
}

/// <summary>
/// A test-only change to the next access token that the app issues, so that a real flow gives the token a real
/// endpoint must refuse: one for another audience, or with the stamp hash or the organisation changed. It is a handler
/// of the server that runs after the claims of the token are prepared and before it is signed. Each test host has its
/// own, holds at most one edit, and applies it once.
/// </summary>
internal sealed class IssuedTokenEdits : IOpenIddictServerHandler<ProcessSignInContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessSignInContext>()
            .UseSingletonHandler<IssuedTokenEdits>()
            .SetOrder(PrepareAccessTokenPrincipal.Descriptor.Order + 100)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    private readonly Lock gate = new();
    private TokenEdit? queued;

    /// <summary>Applies <paramref name="edit"/> to the next access token that is issued.</summary>
    public void Queue(TokenEdit edit)
    {
        lock (gate)
        {
            queued = edit;
        }
    }

    public ValueTask HandleAsync(ProcessSignInContext context)
    {
        // The authorisation code is issued with no access token, so only the exchange of it takes the edit.
        if (context.AccessTokenPrincipal is not { } principal)
        {
            return default;
        }

        TokenEdit edit;
        lock (gate)
        {
            if (queued is not { } next)
            {
                return default;
            }

            edit = next;
            queued = null;
        }

        switch (edit)
        {
            case TokenEdit.AnotherAudience:
                principal.SetAudiences("https://another.example/mcp");
                break;
            case TokenEdit.NoStampHash:
                principal.SetClaim(AssistantTokens.StampHashClaimType, (string?)null);
                break;
            case TokenEdit.AnotherStampHash:
                principal.SetClaim(AssistantTokens.StampHashClaimType, AssistantTokens.StampHash("another stamp"));
                break;
            case TokenEdit.AnotherOrganisation:
                principal.SetClaim(TenantContext.OrganisationIdClaimType, Guid.NewGuid().ToString("D"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(context), edit, null);
        }

        return default;
    }
}
