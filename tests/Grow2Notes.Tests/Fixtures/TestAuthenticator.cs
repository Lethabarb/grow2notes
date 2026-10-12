using System.Buffers.Binary;
using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// A passkey authenticator for the API tests, which run no browser: it holds one passkey, an ES256 key pair that it
/// makes, and answers a sign-in's challenge as a browser at <see cref="Grow2NotesFactory.Origin"/> would, with the
/// credential JSON that the SPA sends to <c>POST /api/auth/passkey</c> (design.md §6.2). Identity's own passkey handler
/// verifies it, as it verifies a real one.
/// </summary>
/// <remarks>
/// A new authenticator's passkey belongs to no account; <see cref="RegisterAsync"/> makes one whose passkey a user
/// holds. <see cref="PasskeySignIn"/> signs a client in with it.
/// </remarks>
internal sealed class TestAuthenticator : IDisposable
{
    // The passkey name that setup stores and no screen shows (D70).
    private const string PasskeyName = "Grow2Notes";

    // The authenticator data's flags (WebAuthn §6.1): the person was present and verified, by a face, a fingerprint or
    // a device PIN, which the passkey options require (design.md §8.4). The passkey is bound to the device, so it is
    // neither backup eligible nor backed up, as Identity checks against the stored passkey.
    private const byte UserPresent = 0x01;
    private const byte UserVerified = 0x04;

    // COSE key labels and values (RFC 9052 §7, RFC 9053 §2.1 and §7): an elliptic-curve key on P-256 for ES256.
    private const int KeyTypeLabel = 1;
    private const int AlgorithmLabel = 3;
    private const int CurveLabel = -1;
    private const int XLabel = -2;
    private const int YLabel = -3;
    private const int EllipticCurveKeyType = 2;
    private const int ES256 = -7;
    private const int P256 = 1;

    // The relying party, App:Origin's host, as the passkey options name it (Program.cs).
    private static readonly string RpId = new Uri(Grow2NotesFactory.Origin).Host;

    private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly byte[] credentialId = RandomNumberGenerator.GetBytes(16);

    // The user entity's ID that setup's creation options give the passkey: the user's ID in UTF-8, which the browser
    // hands back with every assertion, and by which Identity finds the account.
    private readonly byte[] userHandle;

    // An authenticator's signature counter goes up with each assertion, and Identity refuses one that does not.
    private uint signCount;

    /// <summary>An authenticator whose passkey no account holds, with a user handle of its own.</summary>
    public TestAuthenticator()
        : this(Guid.NewGuid().ToString())
    {
    }

    private TestAuthenticator(string userId) => userHandle = Encoding.UTF8.GetBytes(userId);

    /// <summary>
    /// Makes an authenticator and stores its passkey for the user whose ID is <paramref name="userId"/>, through the
    /// app's <c>UserManager.AddOrUpdatePasskeyAsync</c>, as setup stores one (design.md §8.1 step 5), created at the
    /// time of the app's <see cref="TimeProvider"/>.
    /// </summary>
    public static async Task<TestAuthenticator> RegisterAsync(IServiceProvider services, Guid userId)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);

        var authenticator = new TestAuthenticator(await users.GetUserIdAsync(user));
        var stored = await users.AddOrUpdatePasskeyAsync(user, new(
            authenticator.credentialId,
            authenticator.CosePublicKey(),
            createdAt: scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
            signCount: 0,
            transports: ["internal"],
            isUserVerified: true,
            isBackupEligible: false,
            isBackedUp: false,
            // A sign-in never reads the attestation, so none is kept.
            attestationObject: [],
            clientDataJson: [])
        {
            Name = PasskeyName,
        });
        Assert.True(stored.Succeeded, string.Join(" ", stored.Errors.Select(error => error.Code)));
        return authenticator;
    }

    /// <summary>
    /// An assertion over <paramref name="challenge"/>, the base64url challenge of passkey request options, as the SPA
    /// sends it: the credential as <c>JSON.stringify</c> writes it, signed for <see cref="Grow2NotesFactory.Origin"/>
    /// and its host, with the person verified.
    /// </summary>
    public string GetAssertion(string challenge)
    {
        // What the browser collects for the authenticator to sign (WebAuthn §5.8.1).
        var clientDataJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = "webauthn.get",
            challenge,
            origin = Grow2NotesFactory.Origin,
            crossOrigin = false,
        });

        // The relying party's hash, the flags and the signature counter, big-endian (WebAuthn §6.1).
        var authenticatorData = new byte[SHA256.HashSizeInBytes + 1 + sizeof(uint)];
        SHA256.HashData(Encoding.UTF8.GetBytes(RpId), authenticatorData);
        authenticatorData[SHA256.HashSizeInBytes] = UserPresent | UserVerified;
        BinaryPrimitives.WriteUInt32BigEndian(authenticatorData.AsSpan(SHA256.HashSizeInBytes + 1), ++signCount);

        var signature = key.SignData(
            [.. authenticatorData, .. SHA256.HashData(clientDataJson)],
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        return JsonSerializer.Serialize(new
        {
            id = Base64Url.EncodeToString(credentialId),
            rawId = Base64Url.EncodeToString(credentialId),
            type = "public-key",
            authenticatorAttachment = "platform",
            response = new
            {
                clientDataJSON = Base64Url.EncodeToString(clientDataJson),
                authenticatorData = Base64Url.EncodeToString(authenticatorData),
                signature = Base64Url.EncodeToString(signature),
                userHandle = Base64Url.EncodeToString(userHandle),
            },
            clientExtensionResults = new { },
        });
    }

    public void Dispose() => key.Dispose();

    // As an authenticator gives the public key when the passkey is made, and Identity stores it: a COSE key in CTAP2's
    // canonical CBOR.
    private byte[] CosePublicKey()
    {
        var point = key.ExportParameters(includePrivateParameters: false).Q;
        var cbor = new CborWriter(CborConformanceMode.Ctap2Canonical);
        cbor.WriteStartMap(5);
        cbor.WriteInt32(KeyTypeLabel);
        cbor.WriteInt32(EllipticCurveKeyType);
        cbor.WriteInt32(AlgorithmLabel);
        cbor.WriteInt32(ES256);
        cbor.WriteInt32(CurveLabel);
        cbor.WriteInt32(P256);
        cbor.WriteInt32(XLabel);
        cbor.WriteByteString(point.X!);
        cbor.WriteInt32(YLabel);
        cbor.WriteByteString(point.Y!);
        cbor.WriteEndMap();
        return cbor.Encode();
    }
}
