using System.Net;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Users;
using Grow2Notes.Web.Platform.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Setup links for the tests of the setup endpoints (design.md §8.1): each invites a user of its own, as the
/// seeded-rows rule asks (tests/README.md), and reads the user's ID and token from the link's fragment, as the SPA
/// does.
/// </summary>
internal static class SetupLinks
{
    /// <summary>The display name of every user that <see cref="InviteAsync"/> invites.</summary>
    public const string DisplayName = "Sam Taylor";

    /// <summary>The path of the link check, <c>POST /api/auth/setup/start</c>.</summary>
    public const string StartPath = "/api/auth/setup/start";

    /// <summary>
    /// Invites a worker into <paramref name="organisation"/> through the invite core, as <c>admin bootstrap</c> and
    /// Invite user invite someone, with an address that no other test or run uses, and returns the setup link's user
    /// ID and token, read with <c>ParseQuery</c>, which matches the SPA's <c>URLSearchParams</c>.
    /// </summary>
    public static async Task<SetupLink> InviteAsync(
        this WebApplicationFactory<Program> app, SeededOrganisation organisation)
    {
        var email = $"sam.taylor.{Guid.NewGuid():N}@example.org";
        await using var scope = app.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<Invitations>().InviteAsync(
            organisation.OrganisationId, DisplayName, email, UserRole.Worker, organisation.ManagerId);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));
        return Read(result.SetupLink, email);
    }

    /// <summary>
    /// The setup link in <paramref name="email"/>, a setup-link email that the app sent: the one line of its text that
    /// is a link, read as <see cref="InviteAsync"/> reads one.
    /// </summary>
    public static SetupLink FromEmail(EmailMessage email) => Read(
        Assert.Single(
            email.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries),
            line => line.StartsWith("https://", StringComparison.Ordinal)),
        email.To);

    /// <summary>
    /// Starts setup with <paramref name="link"/> as the SPA does, with an antiforgery token fetched first, which it
    /// keeps for the later setup steps, and checks that the start succeeded. Returns that token and the enrolment
    /// cookie that the start set, which <paramref name="client"/>, one from
    /// <see cref="AntiforgeryTokens.CreateHttpsClient"/>, keeps and sends to the later steps.
    /// </summary>
    public static async Task<(string AntiforgeryToken, SetCookieHeaderValue EnrolmentCookie)> StartSetupAsync(
        this HttpClient client, SetupLink link)
    {
        var token = await client.FetchTokenAsync();
        using var response = await client.PostWithTokenAsync(
            StartPath, token, new { userId = link.UserId, token = link.Token });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (token, Assert.Single(response.SetCookies()));
    }

    /// <summary>The security stamp in the row of the user whose ID is <paramref name="userId"/>.</summary>
    public static async Task<string?> SecurityStampAsync(this WebApplicationFactory<Program> app, string userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId);
        Assert.NotNull(user);
        return user.SecurityStamp;
    }

    // With ParseQuery, which matches the SPA's URLSearchParams: + is a space, then %XX is decoded.
    private static SetupLink Read(string setupLink, string email)
    {
        var fragment = QueryHelpers.ParseQuery(setupLink[(setupLink.IndexOf('#', StringComparison.Ordinal) + 1)..]);
        return new(fragment["u"].ToString(), fragment["t"].ToString(), email);
    }
}

/// <summary>A setup link's user ID and token, as the SPA reads them from its fragment, and the user's email.</summary>
internal sealed record SetupLink(string UserId, string Token, string Email);
