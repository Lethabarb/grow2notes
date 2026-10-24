using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// <c>GET /api/reports/daily/{date}/export?format=docx|pdf</c> (design.md §6.5): the day's submitted notes as a Word or
/// PDF file, rendered in memory from one <see cref="DailyReportModel"/> and sent as an attachment. A day with no
/// submitted note in the caller's organisation, including a future day, gets <c>404 report.no_notes</c>. A format that
/// is not exactly <c>docx</c> or <c>pdf</c> gets <c>400</c>. <c>GET /api/reports/daily/{date}/has-notes</c> answers
/// whether the export would find a note, as <c>{date, hasNotes}</c> and nothing more.
/// </summary>
/// <remarks>
/// Both routes have the <see cref="Policies.Manager"/> policy of their own, as §6.5 puts them outside
/// <c>/api/admin</c>, which gives a worker a <c>403</c> and a request with no session the session cookie's <c>401</c>.
/// The tenant query filter keeps the read to the caller's organisation (§5.9 item 2). Nothing is written to disk or
/// storage (A19). The export writes one <c>report.downloaded</c> event once the file is rendered and before it is
/// sent, with no transaction, so it is committed before any byte leaves; a failed write answers <c>500</c> and sends
/// no file. The has-notes check writes no row and no event.
/// </remarks>
internal static class DailyReportEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapDailyReportEndpoint(this RouteGroupBuilder api)
    {
        api.MapGet("/reports/daily/{date:isodate}/export", ExportAsync).RequireAuthorization(Policies.Manager);
        api.MapGet("/reports/daily/{date:isodate}/has-notes", HasNotesAsync).RequireAuthorization(Policies.Manager);
        return api;
    }

    private static async Task<Ok<DailyReportHasNotes>> HasNotesAsync(
        DateOnly date, Grow2NotesDbContext db, CancellationToken cancellationToken)
        => TypedResults.Ok(
            new DailyReportHasNotes(date, await DailyReportModel.HasNotesAsync(db, date, cancellationToken)));

    private static async Task<Results<FileContentHttpResult, BadRequest, ProblemHttpResult>> ExportAsync(
        DateOnly date,
        string? format,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        Grow2NotesDbContext db,
        ITenantContext tenant,
        IAuditWriter audit,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // The bare format, which the status code pages make problem details. The match is ordinal, so PDF is refused.
        if (!ReportFile.IsFormat(format))
        {
            return TypedResults.BadRequest();
        }

        var callerId = Actor.InApp(user, identityOptions.Value).UserId;
        var model = await DailyReportModel.ReadAsync(
            db, tenant.OrganisationId, date, callerId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        if (model is null)
        {
            return ErrorCode.NoNotes.Problem();
        }

        var (bytes, contentType) = ReportFile.Render(ReportLayout.DailyReport(model), format);
        var fileDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        await audit.WriteAsync(
            tenant.OrganisationId,
            callerId,
            AuditEventTypes.Report.Downloaded,
            AuditEntityTypes.Report,
            entityId: null,
            participantId: null,
            new JsonObject { ["date"] = fileDate, ["format"] = format },
            cancellationToken);
        return TypedResults.File(bytes, contentType, $"daily-notes_{fileDate}.{format}");
    }
}

/// <summary>The answer to the has-notes check: the day asked about, and whether it has a submitted note.</summary>
internal sealed record DailyReportHasNotes(DateOnly Date, bool HasNotes);
