using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace Grow2Notes.Web.Features.GuidePrompts;

/// <summary>
/// The handler that reads and replaces an organisation's guide prompts (design.md §4.10, §6.6), as a handler class
/// that takes the <see cref="Actor"/>, the acting user and the channel (mcp-server.md §3.5, convention 1):
/// <c>GET</c> and <c>PUT /api/admin/guide-prompts</c> call it with <see cref="Actor.InApp"/>, and Release 2's
/// <c>get_guide_prompts</c> and <c>set_guide_prompts</c> tools will call it with <see cref="Channel.Mcp"/>.
/// </summary>
/// <remarks>
/// The prompts are a column of the tenant's own <see cref="Organisation"/> row, whose <c>RowVersion</c> is their
/// version (§6.7). The row is found by the tenant's ID from <see cref="ITenantContext"/>, as every other read and write
/// of the request is, so no caller can reach another organisation's prompts (§5.9 item 7), and there is no <c>404</c>
/// to give.
/// </remarks>
internal sealed class GuidePromptsHandler(Grow2NotesDbContext db, ITenantContext tenant, ConfigurationChanges changes)
{
    /// <summary>The guide prompts as stored, with the row's version token.</summary>
    /// <remarks>
    /// Nothing here uses the actor, as reading writes no audit event in the app (design.md §5.4, A28). It is taken so
    /// that every admin handler has the same shape.
    /// </remarks>
    public async Task<GuidePromptsVersion> GetAsync(Actor actor, CancellationToken cancellationToken)
    {
        var organisation = await db.Organisations.AsNoTracking()
            .SingleAsync(o => o.Id == tenant.OrganisationId, cancellationToken);
        return new(organisation.GuidePrompts, RowVersionToken.Of(organisation));
    }

    /// <summary>
    /// Replaces the guide prompts with <paramref name="text"/>, as it is given, and writes one
    /// <c>guide_prompts.updated</c> with the old and new text; or, when the text is already stored, changes nothing and
    /// writes no event (D84). The version is checked in the channel's order (<see cref="IfMatch.CheckFor"/>): for
    /// <see cref="Channel.App"/>, first, so that a missing or stale <c>If-Match</c> is refused whatever the text; for
    /// <see cref="Channel.Mcp"/>, only when the text would change, so that a call that changes nothing answers
    /// <see cref="GuidePromptsSaveResult.Changed"/> <see langword="false"/> whatever version it sent (A132).
    /// </summary>
    /// <param name="actor">Who replaces the prompts, whom the event records.</param>
    /// <param name="ifMatch">
    /// The request's <c>If-Match</c> values, as <c>Headers.IfMatch</c> gives them, or Release 2's
    /// <c>expectedVersion</c> through <see cref="IfMatch.FromExpectedVersion"/>.
    /// </param>
    /// <param name="text">The new prompts, whose length the caller has checked against the column's.</param>
    /// <param name="cancellationToken">Cancels the load and the save.</param>
    /// <returns>
    /// The row's version token once the text is stored, a new one after a save and, when nothing changed, the stored
    /// row's, whatever version was sent; or the code that <see cref="IfMatch.Check"/> gives, or
    /// <see cref="ErrorCode.PreconditionFailed"/> when the row changed after it was loaded, with nothing written.
    /// </returns>
    public async Task<GuidePromptsSaveResult> SaveAsync(
        Actor actor, StringValues ifMatch, string text, CancellationToken cancellationToken)
    {
        var organisation = await db.Organisations.SingleAsync(o => o.Id == tenant.OrganisationId, cancellationToken);
        var version = IfMatch.CheckFor(actor, ifMatch, RowVersionToken.Of(organisation));
        if (version.RefuseNow is { } refused)
        {
            return GuidePromptsSaveResult.Refused(refused);
        }

        var before = Values(organisation);
        organisation.GuidePrompts = text;
        var saved = await changes.SaveAsync(
            actor, AuditEventTypes.GuidePrompts.Updated, AuditEntityTypes.Organisation, organisation.Id,
            participantId: null, before, Values(organisation), refuseIfChanged: version.RefuseIfChanged,
            cancellationToken: cancellationToken);

        // EF Core reads back the version that SQL Server gave the row as it saved it, so after a save this is the new
        // token, and when nothing changed it is the one the If-Match matched.
        return saved.Error is { } error
            ? GuidePromptsSaveResult.Refused(error)
            : GuidePromptsSaveResult.Stored(RowVersionToken.Of(organisation), saved.Changed);
    }

    // The values that the event's Details give, named as design.md §6.6 names the text.
    private static JsonObject Values(Organisation organisation) => new() { ["text"] = organisation.GuidePrompts };
}
