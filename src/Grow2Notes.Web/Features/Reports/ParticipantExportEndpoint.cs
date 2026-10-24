using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// <c>GET /api/reports/participants/{participantId}/export?from=&amp;to=&amp;format=docx|pdf&amp;includeHistory=</c>
/// (design.md §6.5): a participant's submitted notes for a range of dates as a Word or PDF file, rendered in memory
/// from one <see cref="ParticipantExportModel"/> and sent as an attachment.
/// </summary>
/// <remarks>
/// The checks run in order. The query comes first, and a <c>from</c> or <c>to</c> that is not a real date written
/// <c>yyyy-MM-dd</c>, a <c>to</c> before <c>from</c> and a <c>format</c> that is not exactly <c>pdf</c> or
/// <c>docx</c> are answered together as <c>422 validation.failed</c>, before any database read. A participant who is
/// not in the caller's organisation, archived or not, gets a plain <c>404</c>, and a range with no submitted note
/// <c>404 report.no_notes</c>. There is no longest range, and a <c>to</c> after today is taken as entered. The
/// <c>includeHistory</c> flag is bound as a <c>bool?</c>, so absent means false and a value that is not a boolean gets
/// the binder's own <c>400</c>. The route has the <see cref="Policies.Manager"/> policy of its own, which gives a
/// worker a <c>403</c> and a request with no session the session cookie's <c>401</c>; the tenant query filter keeps
/// every read to the caller's organisation (§5.9 item 2). Nothing is written to disk or storage (A19). One
/// <c>participant.exported</c> event is written once the file is rendered and before it is sent, with no
/// transaction, so it is committed before any byte leaves; a failed write answers <c>500</c> and sends no file, and
/// every refusal writes none.
/// </remarks>
internal static class ParticipantExportEndpoint
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapParticipantExportEndpoint(this RouteGroupBuilder api)
    {
        api.MapGet("/reports/participants/{participantId:guid}/export", ExportAsync)
            .RequireAuthorization(Policies.Manager)
            .RequireRateLimiting(RateLimits.Export);
        return api;
    }

    // The messages are for a developer: the SPA checks the same rules first and shows its own words.
    private static async Task<Results<FileContentHttpResult, ProblemHttpResult, NotFound>> ExportAsync(
        Guid participantId,
        string? from,
        string? to,
        string? format,
        bool? includeHistory,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        Grow2NotesDbContext db,
        ITenantContext tenant,
        IAuditWriter audit,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var fromIsDate = TryParseDate(from, out var fromDate);
        if (!fromIsDate)
        {
            errors["from"] = ["From must be a date written yyyy-MM-dd."];
        }

        if (!TryParseDate(to, out var toDate))
        {
            errors["to"] = ["To must be a date written yyyy-MM-dd."];
        }
        else if (fromIsDate && toDate < fromDate)
        {
            errors["to"] = ["To must be the same as or after from."];
        }

        if (!ReportFile.IsFormat(format))
        {
            errors["format"] = ["Format must be pdf or docx."];
        }

        if (errors.Count > 0)
        {
            return ValidationProblems.Fields(errors);
        }

        if (!await db.Participants.AnyAsync(participant => participant.Id == participantId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var callerId = Actor.InApp(user, identityOptions.Value).UserId;
        var history = includeHistory ?? false;
        var model = await ParticipantExportModel.ReadAsync(
            db,
            tenant.OrganisationId,
            participantId,
            fromDate,
            toDate,
            history,
            callerId,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        if (model is null)
        {
            return ErrorCode.NoNotes.Problem();
        }

        var (bytes, contentType) = ReportFile.Render(ReportLayout.ParticipantRecord(model), format!);
        var fileFrom = fromDate.ToString(DateFormat, CultureInfo.InvariantCulture);
        var fileTo = toDate.ToString(DateFormat, CultureInfo.InvariantCulture);

        await audit.WriteAsync(
            tenant.OrganisationId,
            callerId,
            AuditEventTypes.Participant.Exported,
            AuditEntityTypes.Participant,
            entityId: participantId,
            participantId: participantId,
            new JsonObject
            {
                ["from"] = fileFrom,
                ["to"] = fileTo,
                ["format"] = format,
                ["includeHistory"] = history,
            },
            cancellationToken);
        return TypedResults.File(bytes, contentType, $"participant-record_{fileFrom}_to_{fileTo}.{format}");
    }

    private static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
