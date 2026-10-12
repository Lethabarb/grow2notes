using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using QRCoder;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>GET /api/auth/setup/authenticator</c> and <c>GET /api/auth/setup/authenticator/qr.png</c> (design.md §6.2, §8.1
/// step 4), called as the SPA calls them: signed out, over HTTPS, after <c>POST /api/auth/setup/start</c> with a link
/// of the test's own (<see cref="SetupLinks"/>). The endpoint matrix covers callers with no enrolment cookie.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class SetupAuthenticatorEndpointsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string KeyPath = "/api/auth/setup/authenticator";

    private const string QrCodePath = "/api/auth/setup/authenticator/qr.png";

    // The key is read at the invite, before the start, and again after the answer, which changes neither it nor the
    // stamp, so the link still works (S00.03.05's third criterion). Identity's keys are 20 random bytes in base32.
    [Fact]
    public async Task After_the_start_the_key_is_the_one_stored_at_the_invite_with_its_authenticator_URI()
    {
        var link = await InviteAsync();
        var (stamp, key) = await StampAndKeyAsync(link.UserId);
        using var client = factory.CreateHttpsClient();
        await client.StartSetupAsync(link);

        using var response = await client.GetAsync(KeyPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(["sharedKey", "authenticatorUri"], body.EnumerateObject().Select(property => property.Name));
        Assert.Matches("^[A-Z2-7]{32}$", key);
        Assert.Equal(key, body.GetProperty("sharedKey").GetString());
        Assert.Equal(AuthenticatorUri(link.Email, key), body.GetProperty("authenticatorUri").GetString());
        Assert.Equal((stamp, key), await StampAndKeyAsync(link.UserId));
    }

    // The app's QR code of the URI is QRCoder's PNG at error correction level M, with the 4-module quiet zone, which
    // the module matrix includes, and the fewest pixels per module that make it at least 200 px across: so the image
    // is that PNG at its own width's pixels per module, and one pixel fewer per module would make it under 200 px.
    [Fact]
    public async Task After_the_start_the_QR_code_is_a_PNG_of_the_authenticator_URI_at_least_200_px_square()
    {
        var link = await InviteAsync();
        var (stamp, key) = await StampAndKeyAsync(link.UserId);
        using var client = factory.CreateHttpsClient();
        await client.StartSetupAsync(link);

        using var response = await client.GetAsync(QrCodePath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        var png = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        var (width, height) = PngSize(png);
        Assert.Equal(width, height);
        using var qrCode = QRCodeGenerator.GenerateQrCode(
            AuthenticatorUri(link.Email, key), QRCodeGenerator.ECCLevel.M);
        var modules = qrCode.ModuleMatrix.Count;
        Assert.InRange(width, 200, 200 + modules - 1);
        Assert.Equal(new PngByteQRCode(qrCode).GetGraphic(width / modules, drawQuietZones: true), png);
        Assert.Equal((stamp, key), await StampAndKeyAsync(link.UserId));
    }

    [Theory]
    [InlineData(KeyPath)]
    [InlineData(QrCodePath)]
    public async Task Without_the_enrolment_cookie_each_gets_401(string path)
    {
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        await AssertUnauthorizedAsync(response);
    }

    // Made Active as completion leaves a user, so the cookie, still within its 30 minutes, is refused for the status.
    [Theory]
    [InlineData(KeyPath)]
    [InlineData(QrCodePath)]
    public async Task With_the_enrolment_cookie_of_a_user_made_Active_since_the_start_each_gets_401(string path)
    {
        var link = await InviteAsync();
        using var client = factory.CreateHttpsClient();
        await client.StartSetupAsync(link);
        await MakeActiveAsync(link.UserId);

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        await AssertUnauthorizedAsync(response);
    }

    // design.md §8.1 step 4's URI, written out. SetupLinks' addresses hold letters, digits, dots and the @, the one
    // character of them that escaping changes.
    private static string AuthenticatorUri(string email, string key) =>
        $"otpauth://totp/Grow2Notes:{email.Replace("@", "%40", StringComparison.Ordinal)}"
        + $"?secret={key}&issuer=Grow2Notes&digits=6";

    // A PNG opens with its 8-byte signature and then its IHDR chunk: the chunk's 4-byte length and its type, then,
    // first in its data, the image's width and height, 4 bytes each, most significant first.
    private static (int Width, int Height) PngSize(byte[] png)
    {
        byte[] signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.Equal(signature, png[..8]);
        Assert.Equal("IHDR"u8.ToArray(), png[12..16]);
        var size = png.AsSpan(16, 8);
        return (BinaryPrimitives.ReadInt32BigEndian(size), BinaryPrimitives.ReadInt32BigEndian(size[4..]));
    }

    // A refusal sets no cookie.
    private static async Task AssertUnauthorizedAsync(HttpResponseMessage response)
    {
        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(response.SetCookies());
    }

    private Task<SetupLink> InviteAsync() => factory.InviteAsync(sqlServer.Seeded.A);

    // In a scope of its own, as the row stands in the database.
    private async Task<(string Stamp, string Key)> StampAndKeyAsync(string userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId);
        Assert.NotNull(user);
        var key = await users.GetAuthenticatorKeyAsync(user);
        Assert.NotNull(user.SecurityStamp);
        Assert.NotNull(key);
        return (user.SecurityStamp, key);
    }

    // In a scope of its own, as a manager's request would change the user.
    private async Task MakeActiveAsync(string userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId);
        Assert.NotNull(user);
        user.Status = UserStatus.Active;
        var result = await users.UpdateAsync(user);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));
    }
}
