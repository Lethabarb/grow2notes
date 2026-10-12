using System.Text;
using System.Text.Json;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// A passkey handler for the tests of setup's completion (S00.04.02's Notes), in place of Identity's, which verifies a
/// real WebAuthn attestation that a .NET test cannot easily make. Identity's sign-in manager still keeps the ceremony
/// in its two-factor cookie and ends it on the attempt, so the endpoint meets the ceremony as it does in the app. The
/// options hold only the user entity they were made for, which is also the ceremony's state, so an attempt attests
/// that user, as Identity's handler does. Any credential but <see cref="RefusedCredential"/> is accepted, as a made-up
/// passkey with a credential ID of its own. The end-to-end tests make real passkeys, which Identity's handler verifies.
/// </summary>
internal static class TestPasskeyHandler
{
    /// <summary>A credential that the handler accepts, as the SPA sends one: the browser's, written as JSON.</summary>
    public const string Credential = """{"id":"made-up","type":"public-key"}""";

    /// <summary>The one credential that the handler refuses, as Identity's refuses one that does not verify.</summary>
    public const string RefusedCredential = """{"id":"refused","type":"public-key"}""";

    /// <summary>
    /// Hosts the app that <paramref name="builder"/> builds with this handler in place of Identity's, which the sign-in
    /// manager takes from the request's services.
    /// </summary>
    public static IWebHostBuilder UseTestPasskeyHandler(this IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services => services.AddScoped<IPasskeyHandler<ApplicationUser>, Handler>());

    /// <summary>The handler itself, for a test that wraps it.</summary>
    internal sealed class Handler(TimeProvider timeProvider) : IPasskeyHandler<ApplicationUser>
    {
        public Task<PasskeyCreationOptionsResult> MakeCreationOptionsAsync(
            PasskeyUserEntity userEntity, HttpContext httpContext)
        {
            var json = JsonSerializer.Serialize(userEntity);
            return Task.FromResult(
                new PasskeyCreationOptionsResult { CreationOptionsJson = json, AttestationState = json });
        }

        public Task<PasskeyAttestationResult> PerformAttestationAsync(PasskeyAttestationContext context)
        {
            if (context.CredentialJson == RefusedCredential)
            {
                return Task.FromResult(PasskeyAttestationResult.Fail(new("The test refused the credential.")));
            }

            // The sign-in manager passes on the state that MakeCreationOptionsAsync gave, and throws without one.
            var userEntity = JsonSerializer.Deserialize<PasskeyUserEntity>(context.AttestationState!)!;
            var passkey = new UserPasskeyInfo(
                credentialId: Guid.NewGuid().ToByteArray(),
                publicKey: Encoding.UTF8.GetBytes("made-up public key"),
                createdAt: timeProvider.GetUtcNow(),
                signCount: 0,
                transports: ["internal"],
                isUserVerified: true,
                isBackupEligible: false,
                isBackedUp: false,
                attestationObject: [],
                clientDataJson: []);
            return Task.FromResult(PasskeyAttestationResult.Success(passkey, userEntity));
        }

        // Setup makes passkeys only; signing in with one is S00.04.04's.
        public Task<PasskeyRequestOptionsResult> MakeRequestOptionsAsync(
            ApplicationUser? user, HttpContext httpContext) =>
            throw new NotSupportedException();

        public Task<PasskeyAssertionResult<ApplicationUser>> PerformAssertionAsync(PasskeyAssertionContext context) =>
            throw new NotSupportedException();
    }
}
