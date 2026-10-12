using System.Net.Mime;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using QRCoder;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>GET /api/auth/setup/authenticator</c> and <c>GET /api/auth/setup/authenticator/qr.png</c> (design.md §6.2, §8.1
/// step 4): the authenticator key of the user whose <see cref="EnrolmentCookie"/> the request carries, with the URI
/// that adds it to an authenticator app, as a <see cref="SetupAuthenticatorResponse"/>; and that URI as a PNG QR code,
/// for the person to scan on view G.
/// </summary>
/// <remarks>
/// Both are <c>AllowAnonymous</c>, as the person has no session yet, and check the enrolment cookie themselves,
/// answering <c>401</c> without a valid one. Both read the key that the invite stored (§8.1 step 1) and never make a
/// new one, so nothing here changes the user's row: the security stamp, and with it the setup link, stays as it was,
/// and a person who opens the link again sees the same key. Like every <c>/api</c> answer, both are <c>no-store</c>
/// (<see cref="CacheHeaders"/>). The QR code is made here, on the app's own origin, never by a QR service, which
/// would see the key (totp-setup.md).
/// </remarks>
internal static class SetupAuthenticatorEndpoints
{
    // The issuer and the account label's prefix (D42), so that the authenticator app lists the account under the app.
    private const string Issuer = "Grow2Notes";

    // The size at which view G shows the image (sign-in.md component 16), so a browser never scales it up.
    private const int MinimumQrCodePixels = 200;

    /// <summary>Maps both in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapSetupAuthenticatorEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/auth/setup/authenticator", GetKeyAsync).AllowAnonymous();
        api.MapGet("/auth/setup/authenticator/qr.png", GetQrCodeAsync).AllowAnonymous();
        return api;
    }

    private static async Task<Results<Ok<SetupAuthenticatorResponse>, UnauthorizedHttpResult>> GetKeyAsync(
        HttpContext context, UserManager<ApplicationUser> users)
    {
        // With no body, which the status code pages make problem details with no design.md §6.9 code.
        if (await EnrolmentCookie.FindInvitedUserAsync(context, users) is not { } user)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(await AuthenticatorOfAsync(users, user));
    }

    private static async Task<Results<FileContentHttpResult, UnauthorizedHttpResult>> GetQrCodeAsync(
        HttpContext context, UserManager<ApplicationUser> users)
    {
        if (await EnrolmentCookie.FindInvitedUserAsync(context, users) is not { } user)
        {
            return TypedResults.Unauthorized();
        }

        var authenticator = await AuthenticatorOfAsync(users, user);
        return TypedResults.File(QrCodePng(authenticator.AuthenticatorUri), MediaTypeNames.Image.Png);
    }

    // The one place where the URI is built: §8.1 step 4's, with the address escaped as a URI component and nothing
    // more, and the key as stored, whose base32 needs no escaping. It names no algorithm or period, as Identity checks
    // SHA-1 codes of 30-second steps, the apps' defaults.
    private static async Task<SetupAuthenticatorResponse> AuthenticatorOfAsync(
        UserManager<ApplicationUser> users, ApplicationUser user)
    {
        var key = await users.GetAuthenticatorKeyAsync(user) ?? throw new InvalidOperationException(
            "The Invited user has no authenticator key, which the invite stores for every user it creates.");

        // RequireUniqueEmail refuses an account without an address, so every user has one.
        var uri = $"otpauth://totp/{Issuer}:{Uri.EscapeDataString(user.Email!)}?secret={key}&issuer={Issuer}&digits=6";
        return new(key, uri);
    }

    // QRCoder's PngByteQRCode writes the PNG itself, with no System.Drawing, which Linux App Service lacks. Error
    // correction level M, and the 4-module quiet zone on every side that a scanner needs (totp-setup.md), which the
    // module matrix includes; then the fewest pixels per module that make the image at least 200 px across.
    private static byte[] QrCodePng(string text)
    {
        using var data = QRCodeGenerator.GenerateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var modules = data.ModuleMatrix.Count;
        var pixelsPerModule = (MinimumQrCodePixels + modules - 1) / modules;
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule, drawQuietZones: true);
    }
}
