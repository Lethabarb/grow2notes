using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Users;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Platform.OperatorCommands;

/// <summary>
/// <c>admin bootstrap</c> (design.md §7.4): creates an organisation and invites its first manager, in one transaction
/// with its <c>admin.bootstrap</c> event, and prints the manager's setup link. There is no sign-up screen (A1), so this
/// is how each organisation starts. It logs nothing, because the link holds the setup token and the address is
/// personal (§9.5): the link goes to the output writer only, and the command keeps no copy of it.
/// </summary>
internal sealed class BootstrapCommand(
    Grow2NotesDbContext db,
    Invitations invitations,
    IAuditWriter audit,
    ITenantContext tenant,
    TimeProvider timeProvider,
    IConfiguration configuration)
{
    /// <summary>The command's name, after <c>admin</c>.</summary>
    public const string Name = "bootstrap";

    /// <summary>How the command is used, as the operator types it.</summary>
    public const string Usage =
        "Usage: admin bootstrap --organisation \"<name>\" --manager-name \"<name>\" --manager-email <address>";

    private const string OrganisationOption = "--organisation";
    private const string ManagerNameOption = "--manager-name";
    private const string ManagerEmailOption = "--manager-email";

    // Each option, all required, and its longest value: the size of the column that stores it.
    private static readonly (string Name, int MaxLength)[] Options =
    [
        (OrganisationOption, Limits.OrganisationName),
        (ManagerNameOption, Limits.DisplayName),
        (ManagerEmailOption, Limits.Email),
    ];

    /// <summary>
    /// Reads <paramref name="options"/>, then, in one transaction, adds the organisation, invites its first manager
    /// through the invite core, and writes <c>admin.bootstrap</c>; once that has committed, writes the manager's setup
    /// link on <paramref name="output"/>. A refused run says why on <paramref name="error"/> and has written nothing:
    /// an unknown, missing, empty or overlong option and an <c>App:Origin</c> that is missing or not an absolute
    /// address are refused before the transaction, and an address that Identity refuses, such as one that already has
    /// an account (A27), rolls it back.
    /// </summary>
    /// <returns><see cref="AdminCommands.Done"/>, or <see cref="AdminCommands.Refused"/>.</returns>
    public async Task<int> RunAsync(
        IReadOnlyList<string> options, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (!TryRead(options, out var values, out var problem))
        {
            return await AdminCommands.RefuseAsync(error, problem, Usage);
        }

        if (FindOriginProblem() is { } originProblem)
        {
            return await AdminCommands.RefuseAsync(
                error, $"{originProblem}, so no setup link can be made.", usage: null);
        }

        // Set by each attempt, so that once a transient failure has run the work again, it holds the link of the
        // attempt that committed. If the failure came after a commit had reached the server, the address that commit
        // saved refuses the next attempt: no second organisation is added, but the run is refused as having written
        // nothing, which that commit makes untrue.
        string? setupLink = null;
        try
        {
            await db.InTransactionAsync(async ct =>
            {
                var organisation = new Organisation
                {
                    Name = values.OrganisationName,
                    CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                };
                db.Organisations.Add(organisation);
                await db.SaveChangesAsync(ct);

                var invite = await invitations.InviteAsync(
                    organisation.Id, values.ManagerName, values.ManagerEmail, UserRole.Manager, invitedByUserId: null);
                if (!invite.Succeeded)
                {
                    // Thrown, so that the transaction rolls back the organisation saved above.
                    throw new InviteRefusedException(invite.Errors);
                }

                using (tenant.Use(organisation.Id))
                {
                    await audit.WriteAsync(
                        organisation.Id, actorUserId: null, AuditEventTypes.Admin.Bootstrap,
                        AuditEntityTypes.Organisation, organisation.Id, participantId: null,
                        new JsonObject { ["userId"] = invite.User.Id }, ct);
                }

                setupLink = invite.SetupLink;
            }, cancellationToken);
        }
        catch (InviteRefusedException refused)
        {
            return await AdminCommands.RefuseAsync(error, Describe(refused.Errors), usage: null);
        }

        await output.WriteLineAsync(setupLink);
        return AdminCommands.Done;
    }

    // By hand, since three options need no command-line library. An option's value is the argument after it, whatever
    // it is, so a value left out shows as a missing option or as an argument that is not an option.
    private static bool TryRead(
        IReadOnlyList<string> options,
        [NotNullWhen(true)] out Values? values,
        [NotNullWhen(false)] out string? problem)
    {
        values = null;
        var given = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < options.Count; i += 2)
        {
            var option = options[i];
            if (!Options.Any(known => known.Name == option))
            {
                problem = $"\"{option}\" is not an option of admin bootstrap.";
                return false;
            }

            if (i + 1 == options.Count)
            {
                problem = $"{option} has no value.";
                return false;
            }

            if (!given.TryAdd(option, options[i + 1]))
            {
                problem = $"{option} is given more than once.";
                return false;
            }
        }

        foreach (var (option, maxLength) in Options)
        {
            // Spaces alone are empty too, as [Required] has them in a request.
            problem = given.GetValueOrDefault(option) switch
            {
                null => $"{option} is missing.",
                var value when string.IsNullOrWhiteSpace(value) => $"{option} is empty.",
                { Length: var length } when length > maxLength => $"{option} is longer than {maxLength} characters.",
                _ => null,
            };
            if (problem is not null)
            {
                return false;
            }
        }

        values = new(given[OrganisationOption], given[ManagerNameOption], given[ManagerEmailOption]);
        problem = null;
        return true;
    }

    // Checked here, so that a setting the invite core could not make a link from is refused before the transaction,
    // rather than thrown from inside it.
    private string? FindOriginProblem()
    {
        try
        {
            return AppOrigin.Find(configuration) is null ? $"{AppOrigin.Key} is not set" : null;
        }
        catch (UriFormatException)
        {
            return $"{AppOrigin.Key} is not an absolute address";
        }
    }

    // The user name holds the address too (§5.3), so an address in use is refused as a duplicate user name as well.
    private static string Describe(IReadOnlyList<IdentityError> errors) =>
        errors.Any(error => error.Code == nameof(IdentityErrorDescriber.DuplicateEmail))
            ? $"The address in {ManagerEmailOption} already has an account, and an address can have only one."
            : $"Identity refused the manager's account: {string.Join(", ", errors.Select(error => error.Code))}.";

    private sealed record Values(string OrganisationName, string ManagerName, string ManagerEmail);

    private sealed class InviteRefusedException(IReadOnlyList<IdentityError> errors) : Exception
    {
        public IReadOnlyList<IdentityError> Errors { get; } = errors;
    }
}
