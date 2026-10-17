using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Users;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Platform.OperatorCommands;

/// <summary>
/// <c>admin reset-signin &lt;email&gt;</c> (design.md §7.4, §8.6 <i>Break-glass</i>): the way out when no manager of an
/// organisation can sign in to reset another's sign-in, which the last-manager rule leaves to the operator (A26), run
/// once the organisation has confirmed who the person is (ops/break-glass.md). As a manager's Reset sign-in and Resend
/// invite do (<see cref="UserSetupLink"/>), it resets an Active user's sign-in and gives an Invited user a new link,
/// but it skips the last-manager rule and prints the link instead of emailing it. It logs nothing, because the link
/// holds the setup token and the address is personal (§9.5): the link goes to the output writer only.
/// </summary>
internal sealed class ResetSignInCommand(
    Grow2NotesDbContext db,
    UserManager<ApplicationUser> users,
    SignInReset signInReset,
    SetupLinkIssuer setupLinks,
    IAuditWriter audit,
    ITenantContext tenant,
    IConfiguration configuration)
{
    /// <summary>The command's name, after <c>admin</c>.</summary>
    public const string Name = "reset-signin";

    /// <summary>How the command is used, as the operator types it.</summary>
    public const string Usage = "Usage: admin reset-signin <email>";

    /// <summary>
    /// Reads the one address in <paramref name="arguments"/>, then, in one transaction, finds its user in any
    /// organisation (A27), letter case aside, and makes them the tenant (§5.9 item 1); resets their sign-in
    /// (<see cref="SignInReset.ResetAsync"/>) if they are Active, which ends every session of theirs, or gives them a
    /// new key and link (<see cref="SetupLinkIssuer.IssueAsync"/>) if they are Invited, either of which ends every link
    /// made for them before; and writes <c>admin.signin_reset</c>, with no actor and no details, as the user's ID says
    /// whose sign-in it was. Once that has committed, it writes the new setup link on <paramref name="output"/>. A
    /// refused run says why on <paramref name="error"/> and has written nothing: no address, more than one argument,
    /// an empty address, and an <c>App:Origin</c> that is missing or not an absolute address are refused before the
    /// transaction; an address with no account, a Deactivated user, who is reactivated in the app first (D75), and a
    /// user changed in the app since the command loaded them, inside it.
    /// </summary>
    /// <returns><see cref="AdminCommands.Done"/>, or <see cref="AdminCommands.Refused"/>.</returns>
    public async Task<int> RunAsync(
        IReadOnlyList<string> arguments, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        // Spaces alone are empty too, as admin bootstrap has them.
        var argumentProblem = arguments switch
        {
            [] => "No address was given.",
            [_, _, ..] => "More than one argument was given, but admin reset-signin takes one address.",
            [var email] when string.IsNullOrWhiteSpace(email) => "The address is empty.",
            _ => null,
        };
        if (argumentProblem is not null)
        {
            return await AdminCommands.RefuseAsync(error, argumentProblem, Usage);
        }

        if (AdminCommands.FindOriginProblem(configuration) is { } originProblem)
        {
            return await AdminCommands.RefuseAsync(error, originProblem, usage: null);
        }

        // Set by each attempt, as a transient failure runs the whole work again, so that it holds the outcome of the
        // attempt that committed.
        Outcome outcome = null!;
        await db.InTransactionAsync(
            async ct => outcome = await ResetAsync(arguments[0], ct), cancellationToken);
        if (outcome.Refusal is not null)
        {
            return await AdminCommands.RefuseAsync(error, outcome.Refusal, usage: null);
        }

        await output.WriteLineAsync(outcome.SetupLink);
        return AdminCommands.Done;
    }

    private async Task<Outcome> ResetAsync(string email, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            return Outcome.Refused("No account has that address.");
        }

        if (user.Status == UserStatus.Deactivated)
        {
            return Outcome.Refused("The account is Deactivated, so a manager reactivates it in the app first.");
        }

        using (tenant.Use(user.OrganisationId))
        {
            // No link means that the app changed the user since they were loaded, and nothing was saved.
            var setupLink = user.Status == UserStatus.Active
                ? await signInReset.ResetAsync(user)
                : await setupLinks.IssueAsync(user);
            if (setupLink is null)
            {
                return Outcome.Refused("The account changed while the command ran, so run it again.");
            }

            // By the user's ID alone: the address stays out of the audit trail (A28).
            await audit.WriteAsync(
                user.OrganisationId, actorUserId: null, AuditEventTypes.Admin.SignInReset, AuditEntityTypes.User,
                user.Id, participantId: null, details: null, cancellationToken);
            return new(setupLink, Refusal: null);
        }
    }

    private sealed record Outcome(string? SetupLink, string? Refusal)
    {
        public static Outcome Refused(string reason) => new(SetupLink: null, reason);
    }
}
