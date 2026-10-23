using System.Buffers.Text;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Notes;
using Grow2Notes.Web.Features.Users;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static System.Net.HttpStatusCode;

namespace Grow2Notes.Tests;

/// <summary>
/// The endpoint matrix (design.md §2, §9.1, §9.2): a row for each endpoint the app maps, with the status that each
/// <see cref="Caller"/> gets from it. <see cref="EndpointMatrixTests"/> calls every endpoint in the app's
/// <c>EndpointDataSource</c> as each caller and checks the status against the endpoint's row, and the answer too for a
/// row that checks it, and an endpoint with no row, or a row with no endpoint, fails the run. So each story that maps
/// an endpoint adds its row here (tests/README.md).
/// </summary>
internal static class EndpointMatrix
{
    /// <summary>
    /// The route of the one row for every endpoint that <c>MapStaticAssets</c> maps, a file of the SPA build each,
    /// whose names change with each build.
    /// </summary>
    public const string StaticAssets = "(MapStaticAssets)";

    /// <summary>
    /// The methods of an endpoint that names none, such as one mapped with <c>Map</c> or <c>MapFallback</c>, which
    /// answers every method; the matrix calls it with GET, POST, PUT and DELETE.
    /// </summary>
    public static readonly IReadOnlyList<string> AnyMethod = [];

    /// <summary>
    /// The option of a request to the caller's unfinished drafts that names the participant for whom the call first
    /// started a draft of A's worker's, for the row's answer check, as <see cref="FlaggedInA"/> is for the To review
    /// list's.
    /// </summary>
    public static readonly HttpRequestOptionsKey<Guid> DraftedInA = new(nameof(DraftedInA));

    /// <summary>
    /// The option of a request to the To review list that names the participant whose note the call first flagged in
    /// A, for the row's answer check, which is given the response, and through it the request, but not the call.
    /// </summary>
    public static readonly HttpRequestOptionsKey<Guid> FlaggedInA = new(nameof(FlaggedInA));

    private static readonly IReadOnlyList<string> Get = ["GET"];

    private static readonly IReadOnlyList<string> GetOrHead = ["GET", "HEAD"];

    private static readonly IReadOnlyList<string> Post = ["POST"];

    private static readonly IReadOnlyList<string> Put = ["PUT"];

    private static readonly IReadOnlyList<string> Delete = ["DELETE"];

    private static readonly HttpMethod[] CalledForAnyMethod =
        [HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete];

    /// <summary>The rows, one for each endpoint, found by the methods it names and its route pattern.</summary>
    public static IReadOnlyList<EndpointRow> Rows { get; } =
    [
        // The SPA, for everyone, so a signed-out user gets the sign-in page: each file of the build, and index.html at
        // each client route.
        //  Methods    Route                     Signed out A's worker A's manager B's manager Request
        new(GetOrHead, StaticAssets,             OK,        OK,        OK,         OK),
        new(GetOrHead, "{*path:nonfile}",        OK,        OK,        OK,         OK,         At("/")),

        // A request that no other endpoint answers is a 404, never a 401: a path with a file extension that is no
        // file of the build, such as /favicon.ico, or a client route called with a method other than GET or HEAD.
        new(AnyMethod, "{*path}",                NotFound,  NotFound,  NotFound,   NotFound,   At("/favicon.ico")),

        // An unknown /api path is a 404 for every method, never the SPA page or a 401 (design.md §6.1). It is outside
        // the /api group, so it needs no antiforgery token either.
        new(AnyMethod, "/api/{**rest}",          NotFound,  NotFound,  NotFound,   NotFound,   At("/api/no-such-path")),

        // App Service's health check and the deploy's smoke test send no credentials. Readiness runs the database
        // check, which passes here.
        new(AnyMethod, "/healthz",               OK,        OK,        OK,         OK),
        new(AnyMethod, "/healthz/ready",         OK,        OK,        OK,         OK),

        // The API (design.md §6), in the /api group. The sign-in page needs an antiforgery token before anyone is
        // signed in (§6.2). Each signed-in user gets themselves from /api/auth/me, in their own organisation.
        new(Get,       "/api/auth/antiforgery",  NoContent, NoContent, NoContent,  NoContent),
        new(Get,       "/api/auth/me",           Unauthorized, OK,     OK,         OK),

        // Each signed-in user signs out of their own session (§8.5). The test-only sign-in has no session cookie, so a
        // call ends nothing: it adds one auth.signed_out to the caller's organisation and changes no seeded row
        // (tests/README.md).
        new(Post,      "/api/auth/logout",       Unauthorized, NoContent, NoContent, NoContent),

        // Each signed-in user may press Stay signed in on the warning before the idle sign-out (§8.5): the ping changes
        // nothing, and the session check renews the session as on any request.
        new(Post,      "/api/auth/ping",         Unauthorized, NoContent, NoContent, NoContent),

        // Setup starts from an invite's link, before anyone is signed in (§8.1 step 3), and a made-up link gets the
        // one answer of a link that does not check, whoever calls.
        new(Post,      "/api/auth/setup/start",  Gone,      Gone,      Gone,       Gone,
            WithJson("/api/auth/setup/start", new { userId = Guid.NewGuid(), token = "made-up" })),

        // Each later setup step needs the enrolment cookie that the start issues, which no caller here has: a session
        // does not stand in for it (§8.1 step 3).
        new(Get,       "/api/auth/setup/authenticator", Unauthorized, Unauthorized, Unauthorized, Unauthorized),
        new(Get,       "/api/auth/setup/authenticator/qr.png", Unauthorized, Unauthorized, Unauthorized, Unauthorized),
        new(Post,      "/api/auth/setup/password", Unauthorized, Unauthorized, Unauthorized, Unauthorized,
            WithJson("/api/auth/setup/password", new { password = "made-up password", totpCode = "123456" })),
        new(Post,      "/api/auth/setup/passkey/options", Unauthorized, Unauthorized, Unauthorized, Unauthorized),
        new(Post,      "/api/auth/setup/passkey", Unauthorized, Unauthorized, Unauthorized, Unauthorized,
            WithJson("/api/auth/setup/passkey", new { credentialJson = "{}", name = "Grow2Notes" })),

        // Anyone may ask for the options to sign in with a passkey (§6.2), signed in or not: they name no user. Anyone
        // may sign in with a passkey too, and a passkey that no account holds is refused, whoever sends it.
        new(Post,      "/api/auth/passkey/options", OK,     OK,        OK,         OK),
        new(Post,      "/api/auth/passkey",      Unauthorized, Unauthorized, Unauthorized, Unauthorized,
            WithAPasskeyNoAccountHolds),

        // Anyone may sign in with a password too, and an email that no account holds is refused, whoever sends it,
        // before any password is checked, so the call changes nothing.
        new(Post,      "/api/auth/login",        Unauthorized, Unauthorized, Unauthorized, Unauthorized,
            WithJson("/api/auth/login", new { email = $"{Guid.NewGuid():N}@example.org", password = "made-up" })),

        // The code step reads its account from the two-factor cookie that a password step sets, which no caller here
        // has, so a made-up code is refused, whoever sends it, with nothing checked or written (§8.2).
        new(Post,      "/api/auth/login/totp",   Unauthorized, Unauthorized, Unauthorized, Unauthorized,
            WithJson("/api/auth/login/totp", new { code = "123456" })),

        // The Today list (§6.3), for every signed-in user, each of whom gets their own organisation's active
        // participants: its participants hold the caller's own organisation's seeded participant and not the other's
        // (§5.9 item 7).
        new(Get,       "/api/today",             Unauthorized, OK,     OK,         OK,
            Answer: HoldsTheCallersOwnSeeded(
                "participant", organisation => [organisation.ParticipantId], listedIn: "participants")),

        // The caller's own unfinished drafts (§6.3), for every signed-in user. No seeded row is a draft, so each call
        // first starts one of A's worker's in A, whose participant its DraftedInA option names: A's worker's list holds
        // it, and neither A's manager's, whose working copy it is not, nor B's manager's, which holds nothing of A's
        // (§5.9 item 7); reading writes nothing (A28).
        new(Get,       "/api/me/drafts",         Unauthorized, OK,     OK,         OK,
            MyDraftsAfterADraftStartedInA, HoldsTheDraftStartedInAForAsWorkerAlone),

        // A participant's header (§6.3), for every signed-in user, at A's seeded participant, whom B's manager does not
        // find (§9.2).
        new(Get,       "/api/participants/{participantId:guid}", Unauthorized, OK, OK, NotFound,
            call => Task.FromResult(
                new HttpRequestMessage(call.Method, $"/api/participants/{call.Seeded.A.ParticipantId}"))),

        // A note (§6.3), for every signed-in user. Each call reads today's note of a participant whom its request first
        // adds to A, submitted by A's worker, so each of A's users reads it, and reading writes nothing (A28). B's
        // manager does not find the participant (§9.2).
        new(Get,       "/api/participants/{participantId:guid}/notes/{noteDate:isodate}", Unauthorized, OK, OK,
            NotFound, ANoteSubmittedInA),

        // A submitted note's versions (§6.3), for managers. Each call lists the versions of a note that its request
        // first adds to A, submitted by A's worker, so A's manager gets the one version and A's worker 403. B's manager
        // does not find the participant (§9.2). Reading writes nothing (A28).
        new(Get,       "/api/participants/{participantId:guid}/notes/{noteDate:isodate}/versions", Unauthorized,
            Forbidden, OK, NotFound, TheVersionsOfANoteSubmittedInA),

        // One version of it (§6.3), for managers, the same way.
        new(Get,
            "/api/participants/{participantId:guid}/notes/{noteDate:isodate}/versions/{versionNumber:int:min(1)}",
            Unauthorized, Forbidden, OK, NotFound, VersionOneOfANoteSubmittedInA),

        // A participant's history (§6.3), for every signed-in user, at A's seeded participant, whose notes no test
        // starts (tests/README.md), so each of A's users gets an empty page, and reading writes nothing (A28). B's
        // manager does not find the participant (§9.2).
        new(Get,       "/api/participants/{participantId:guid}/notes", Unauthorized, OK, OK, NotFound,
            call => Task.FromResult(
                new HttpRequestMessage(call.Method, $"/api/participants/{call.Seeded.A.ParticipantId}/notes"))),

        // The note form (§6.3), for every signed-in user, at A's seeded participant's note for today, which no test
        // starts (tests/README.md), so each of A's users gets the template and opening it writes nothing (§3.3). B's
        // manager does not find the participant (§9.2).
        new(Get,       "/api/participants/{participantId:guid}/notes/{noteDate:isodate}/draft", Unauthorized, OK, OK,
            NotFound, TodaysNoteFormOfTheSeededParticipantOfA),

        // The note's autosave (§6.3), for every signed-in user. Each call is the first save of today's note of a
        // participant whom its request first adds to A, as a call commits (tests/README.md), so each of A's users
        // starts a note of their own and gets 201. B's manager does not find the participant (§9.2).
        new(Put,       "/api/participants/{participantId:guid}/notes/{noteDate:isodate}/draft", Unauthorized, Created,
            Created, NotFound, StartingTodaysNoteOfAParticipantOfItsOwn),

        // Submit (§6.3), for every signed-in user. Each call submits a draft that A's worker started for today, of a
        // participant whom its request first adds to A, as a call commits (tests/README.md), with a key of its own, so
        // A's worker, its author, gets 201, and A's manager 409 note.in_progress_by_other, as only the author creates
        // version 1 (§5.8 step 2). B's manager does not find the participant (§9.2).
        new(Post,      "/api/participants/{participantId:guid}/notes/{noteDate:isodate}/versions", Unauthorized,
            Created, Conflict, NotFound, SubmittingADraftOfTheWorkerOfA),

        // Discard draft (§6.3), for every signed-in user. Each call discards a draft that A's worker started for today,
        // of a participant whom its request first adds to A, as a call commits (tests/README.md), so A's worker, its
        // author, and A's manager each get 204 (§3.4). B's manager does not find the participant (§9.2).
        new(Post,      "/api/participants/{participantId:guid}/notes/{noteDate:isodate}/discard", Unauthorized,
            NoContent, NoContent, NotFound, DiscardingADraftOfTheWorkerOfA),

        // Mark reviewed (§6.4), for managers. Each call reviews a note that A's worker submitted flagged, of a
        // participant whom its request first adds to A, as a call commits (tests/README.md), so A's manager gets 201
        // and A's worker 403. B's manager does not find the participant (§9.2).
        new(Post,      "/api/participants/{participantId:guid}/notes/{noteDate:isodate}/reviews", Unauthorized,
            Forbidden, Created, NotFound, ReviewingAFlaggedNoteOfTheWorkerOfA),

        // Cancel (§6.3, A71), for every signed-in user. Each call cancels the pending edit of today's note of a
        // participant whom its request first adds to A, submitted by A's worker, and A's worker and A's manager each
        // hold a pending edit of it, as a call commits (tests/README.md), so each gets 204, and the answer is never
        // 403. B's manager does not find the participant (§9.2).
        new(Delete,    "/api/participants/{participantId:guid}/notes/{noteDate:isodate}/draft", Unauthorized,
            NoContent, NoContent, NotFound, CancellingAPendingEditOfTheWorkerOfAAndTheManagerOfA),

        // The To review list (§6.4), outside /api/admin with the Manager policy of its own, so for managers alone. The
        // seeded organisations have no flagged note, so each call first flags one in A, and A's manager's list holds
        // it and B's manager's does not (§5.9 item 7); reading writes nothing (A28).
        new(Get,       "/api/reviews",           Unauthorized, Forbidden, OK,     OK,
            ToReviewAfterANoteFlaggedInA, HoldsTheNoteFlaggedInAForAsManagerAlone),

        // The daily report (§6.5), outside /api/admin with the Manager policy of its own, so for managers alone. Each
        // call first submits a note in A on a day that no test seeds in B, so A's manager gets the file and B's manager
        // gets 404 for a day with no notes of B's (§5.9 item 7).
        new(Get,       "/api/reports/daily/{date:isodate}/export", Unauthorized, Forbidden, OK, NotFound,
            TheDailyReportOfANoteSubmittedInA),

        // The user list (§6.6), in the /api/admin group, which only managers may call (§2). Each manager's list holds
        // their own organisation's users and none of the other's (§5.9 items 5 and 7).
        new(Get,       "/api/admin/users",       Unauthorized, Forbidden, OK, OK,
            Answer: HoldsTheCallersOwnSeeded("user", organisation => [organisation.ManagerId, organisation.WorkerId])),

        // Each manager invites into their own organisation, which a call commits, so each call invites an address of
        // its own (tests/README.md).
        new(Post,      "/api/admin/users",       Unauthorized, Forbidden, Created, Created,
            AnInviteOfANewAddress),

        // Each call renames, resends the invite of, deactivates or reactivates a user it first invites into A, as a
        // call commits (tests/README.md), with the user's ETag. B's manager does not find A's user (§9.2).
        // Reactivating someone who is not Deactivated changes nothing, and answers as a reactivation does.
        new(Put,       "/api/admin/users/{id:guid}", Unauthorized, Forbidden, OK, NotFound,
            ARenameOfANewInviteInA),
        new(Post,      "/api/admin/users/{id:guid}/setup-link", Unauthorized, Forbidden, NoContent, NotFound,
            OnANewInviteInA("setup-link")),
        new(Post,      "/api/admin/users/{id:guid}/deactivate", Unauthorized, Forbidden, NoContent, NotFound,
            OnANewInviteInA("deactivate")),
        new(Post,      "/api/admin/users/{id:guid}/reactivate", Unauthorized, Forbidden, NoContent, NotFound,
            OnANewInviteInA("reactivate")),

        // The guide prompts (§6.6), each manager's own organisation's, as the tenant is the caller's (§5.9 item 7). A
        // PUT with no If-Match is refused before anything is saved, so the calls change nothing.
        new(Get,       "/api/admin/guide-prompts", Unauthorized, Forbidden, OK, OK),
        new(Put,       "/api/admin/guide-prompts", Unauthorized, Forbidden, PreconditionRequired, PreconditionRequired,
            WithJson("/api/admin/guide-prompts", new { text = "What did they enjoy today?" })),

        // The participant list (§6.6), in the same group. Each manager's list holds their own organisation's seeded
        // participant and not the other's.
        new(Get,       "/api/admin/participants", Unauthorized, Forbidden, OK, OK,
            Answer: HoldsTheCallersOwnSeeded("participant", organisation => [organisation.ParticipantId])),

        // Adding a participant (§6.6), in the same group. The policy refuses A's worker before the handler runs, so
        // the worker's call adds no one. Each manager's call adds a made-up participant to their own organisation,
        // which the seeded-rows rule allows (tests/README.md), and loads nothing of another's, so B's manager gets
        // what A's does.
        new(Post,      "/api/admin/participants", Unauthorized, Forbidden, Created, Created,
            WithJson("/api/admin/participants",
                new { givenName = "Matrix", familyName = "Caller", dateOfBirth = "1990-01-01" })),

        // Correcting a participant (§6.6), in the same group. Each call corrects a participant of its own, whom its
        // request first adds to A, so B's manager, to whom A's participants are not found, gets 404 (§9.2).
        new(Put,       "/api/admin/participants/{id:guid}", Unauthorized, Forbidden, OK, NotFound,
            CorrectingAParticipantOfItsOwn),

        // Archiving and restoring a participant (§6.6), in the same group, as the correction is: each call archives an
        // active participant, or restores an archived one, of its own in A, so B's manager gets 404.
        new(Post,      "/api/admin/participants/{id:guid}/archive", Unauthorized, Forbidden, NoContent, NotFound,
            call => ChangingTheStatusOfAParticipantOfItsOwn(call, "archive", archived: false)),
        new(Post,      "/api/admin/participants/{id:guid}/restore", Unauthorized, Forbidden, NoContent, NotFound,
            call => ChangingTheStatusOfAParticipantOfItsOwn(call, "restore", archived: true)),

        // A participant's goals (§6.6), in the same group, at A's seeded participant, whom B's manager does not find
        // (§9.2): the list belongs to that one participant, so the 404 is its isolation check.
        new(Get,       "/api/admin/participants/{participantId:guid}/goals", Unauthorized, Forbidden, OK, NotFound,
            call => Task.FromResult(
                new HttpRequestMessage(call.Method, $"/api/admin/participants/{call.Seeded.A.ParticipantId}/goals"))),

        // Adding a goal (§6.6), in the same group. Each call adds a goal to a participant of its own, whom its request
        // first adds to A, so B's manager, to whom A's participants are not found, gets 404 (§9.2).
        new(Post,      "/api/admin/participants/{participantId:guid}/goals", Unauthorized, Forbidden, Created, NotFound,
            AddingAGoalToAParticipantOfItsOwn),

        // Rewording a goal (§6.6), in the same group. Each call rewords a goal of its own, which its request first adds
        // to a participant whom it adds to A, so B's manager, to whom A's goals are not found, gets 404 (§9.2).
        new(Put,       "/api/admin/goals/{goalId:guid}", Unauthorized, Forbidden, OK, NotFound,
            RewordingAGoalOfItsOwn),

        // Reordering a participant's goals (§6.6), in the same group. Each call reorders the active goals of a
        // participant of its own, whom its request first adds to A, so B's manager, to whom A's participants are not
        // found, gets 404 (§9.2).
        new(Put,       "/api/admin/participants/{participantId:guid}/goals/order", Unauthorized, Forbidden, NoContent,
            NotFound, ReorderingTheGoalsOfAParticipantOfItsOwn),

        // Archiving and restoring a goal (§6.6), in the same group, as the reword is: each call archives an active
        // goal, or restores an archived one, of its own, which its request first adds to a participant whom it adds to
        // A, so B's manager gets 404.
        new(Post,      "/api/admin/goals/{goalId:guid}/archive", Unauthorized, Forbidden, NoContent, NotFound,
            call => ChangingThePlaceOfAGoalOfItsOwn(call, "archive", archived: false)),
        new(Post,      "/api/admin/goals/{goalId:guid}/restore", Unauthorized, Forbidden, NoContent, NotFound,
            call => ChangingThePlaceOfAGoalOfItsOwn(call, "restore", archived: true)),

        // The common item groups with their items (§6.6), in the same group. The list loads no record of A's, so B's
        // manager gets B's own list, which must hold B's seeded Every note and not A's; tests add other groups to the
        // seeded organisations, so the seeded Every note groups alone are looked for.
        new(Get,       "/api/admin/common-item-groups", Unauthorized, Forbidden, OK, OK,
            Answer: HoldsTheCallersOwnSeeded("common item group", organisation => [organisation.EveryNoteGroupId])),

        // Adding a common item group (§6.6), in the same group, as adding a participant is: the policy refuses A's
        // worker before the handler runs, so the worker's call adds no group, and each manager's call adds a made-up
        // group at the end of their own organisation's, which the seeded-rows rule allows (tests/README.md), and loads
        // nothing of another's, so B's manager gets what A's does.
        new(Post,      "/api/admin/common-item-groups", Unauthorized, Forbidden, Created, Created,
            WithJson("/api/admin/common-item-groups", new { name = "Community outing" })),

        // Renaming a common item group (§6.6), in the same group. Each call renames a group of its own, which its
        // request first adds to A, never the seeded Every note, so B's manager, to whom A's groups are not found, gets
        // 404 (§9.2).
        new(Put,       "/api/admin/common-item-groups/{groupId:guid}", Unauthorized, Forbidden, OK, NotFound,
            RenamingAGroupOfItsOwn),

        // Reordering the common item groups (§6.6), in the same group. Each call adds two groups of its own to A and
        // sends them with A's other active groups but Every note, so A's manager's call moves them. The IDs are checked
        // against the caller's own organisation's groups, so B's manager, whose groups those are not, gets
        // 422 config.order_mismatch, and A's order stays as it was (§9.2).
        new(Put,       "/api/admin/common-item-groups/order", Unauthorized, Forbidden, NoContent, UnprocessableEntity,
            ReorderingTheGroupsWithTwoOfItsOwn),

        // Archiving and restoring a common item group (§6.6), in the same group, as the rename is: each call archives
        // an active group, or restores an archived one, of its own, which its request first adds to A, never the seeded
        // Every note, so B's manager, to whom A's groups are not found, gets 404 (§9.2).
        new(Post,      "/api/admin/common-item-groups/{groupId:guid}/archive", Unauthorized, Forbidden, NoContent,
            NotFound, call => ChangingTheStateOfAGroupOfItsOwn(call, "archive", archived: false)),
        new(Post,      "/api/admin/common-item-groups/{groupId:guid}/restore", Unauthorized, Forbidden, NoContent,
            NotFound, call => ChangingTheStateOfAGroupOfItsOwn(call, "restore", archived: true)),

        // Adding a common item (§6.6), in the same group. Each call adds an item to a group of its own, which its
        // request first adds to A, so B's manager, to whom A's groups are not found, gets 404 (§9.2).
        new(Post,      "/api/admin/common-item-groups/{groupId:guid}/items", Unauthorized, Forbidden, Created, NotFound,
            AddingAnItemToAGroupOfItsOwn),

        // Rewording a common item (§6.6), in the same group. Each call rewords an item of its own, which its request
        // first adds to a group that it adds to A, so B's manager, to whom A's items are not found, gets 404 (§9.2).
        new(Put,       "/api/admin/common-items/{id:guid}", Unauthorized, Forbidden, OK, NotFound,
            RewordingAnItemOfItsOwn),

        // Reordering a group's common items (§6.6), in the same group. Each call reorders the active items of a group
        // of its own, which its request first adds to A, so B's manager, to whom A's groups are not found, gets 404
        // (§9.2).
        new(Put,       "/api/admin/common-item-groups/{groupId:guid}/items/order", Unauthorized, Forbidden, NoContent,
            NotFound, ReorderingTheItemsOfAGroupOfItsOwn),

        // Archiving and restoring a common item (§6.6), in the same group, as the reword is: each call archives an
        // active item, or restores an archived one, of its own, which its request first adds to a group that it adds
        // to A, so B's manager gets 404.
        new(Post,      "/api/admin/common-items/{id:guid}/archive", Unauthorized, Forbidden, NoContent, NotFound,
            call => ChangingThePlaceOfAnItemOfItsOwn(call, "archive", archived: false)),
        new(Post,      "/api/admin/common-items/{id:guid}/restore", Unauthorized, Forbidden, NoContent, NotFound,
            call => ChangingThePlaceOfAnItemOfItsOwn(call, "restore", archived: true)),
    ];

    // Two rows for one endpoint stop the type initializer here, naming the endpoint.
    private static readonly Dictionary<string, EndpointRow> RowsByKey =
        Rows.ToDictionary(row => KeyOf(row.Methods, row.Route));

    /// <summary>
    /// Every call of the endpoints of the app whose services are <paramref name="services"/>: each endpoint with each
    /// method it names, or with GET, POST, PUT and DELETE when it names none. Each call carries the services and
    /// <paramref name="seeded"/>, with which a row's builder adds the record that the call acts on.
    /// </summary>
    public static IEnumerable<EndpointCall> CallsTo(IServiceProvider services, SeededOrganisations seeded) =>
        from endpoint in services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
        let methods = MethodsOf(endpoint)
        from method in methods is [] ? CalledForAnyMethod : methods.Select(name => HttpMethod.Parse(name))
        select new EndpointCall(method, endpoint, services, seeded);

    /// <summary>The row of <paramref name="endpoint"/>, or <see langword="null"/> when the matrix has none.</summary>
    public static EndpointRow? RowOf(RouteEndpoint endpoint) => RowsByKey.GetValueOrDefault(KeyOf(endpoint));

    /// <summary>
    /// The endpoints in <paramref name="endpoints"/> that have no row, and the rows that no endpoint there has, each
    /// named by its methods and route.
    /// </summary>
    public static (IReadOnlyList<string> EndpointsWithNoRow, IReadOnlyList<string> RowsWithNoEndpoint) Gaps(
        EndpointDataSource endpoints)
    {
        var endpointKeys = endpoints.Endpoints.OfType<RouteEndpoint>().Select(KeyOf).ToList();
        return ([.. endpointKeys.Except(RowsByKey.Keys)], [.. RowsByKey.Keys.Except(endpointKeys)]);
    }

    /// <summary>The methods that <paramref name="endpoint"/> names, or <see cref="AnyMethod"/>.</summary>
    public static IReadOnlyList<string> MethodsOf(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? AnyMethod;

    /// <summary><paramref name="row"/>'s methods and route, as <see cref="Gaps"/> names it.</summary>
    public static string NameOf(EndpointRow row) => KeyOf(row.Methods, row.Route);

    /// <summary>
    /// Whether <paramref name="route"/>, an endpoint's route pattern or a row's route, starts with <c>/api/admin</c>,
    /// under which only managers may call (design.md §2, §6.6). It is compared whatever its case, and with or without
    /// a leading slash, as routing matches a path.
    /// </summary>
    public static bool IsAdmin(string route) =>
        ("/" + route.TrimStart('/')).StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase);

    private static string KeyOf(RouteEndpoint endpoint) => KeyOf(
        MethodsOf(endpoint),
        endpoint.Metadata.GetMetadata<StaticAssetDescriptor>() is null
            ? endpoint.RoutePattern.RawText ?? ""
            : StaticAssets);

    // The methods in any order, because a row lists them as written and an endpoint as mapped.
    private static string KeyOf(IReadOnlyList<string> methods, string route) =>
        methods is []
            ? $"any method {route}"
            : $"{string.Join(" or ", methods.Order(StringComparer.Ordinal))} {route}";

    // For a route with parameters: a path that its endpoint answers.
    private static Func<EndpointCall, Task<HttpRequestMessage>> At(string path) =>
        call => Task.FromResult(new HttpRequestMessage(call.Method, path));

    // For an endpoint that reads a JSON body: a request to the path that it answers, with a body that it reads.
    private static Func<EndpointCall, Task<HttpRequestMessage>> WithJson(string path, object body) =>
        call => Task.FromResult(new HttpRequestMessage(call.Method, path) { Content = JsonContent.Create(body) });

    // The caller's unfinished drafts, asked for once the request has added to A a participant of its own, whom its
    // DraftedInA option names, with a draft of A's worker's for today, as its first save leaves it. The draft's start
    // time is made up (A35).
    private static async Task<HttpRequestMessage> MyDraftsAfterADraftStartedInA(EndpointCall call)
    {
        var a = call.Seeded.A;
        var participant = await call.Services.AddParticipantAsync(a);
        var today = call.Services.GetRequiredService<MelbourneClock>().Today();
        await call.Services.AddDraftAsync(
            a, new(participant.Id, today, a.WorkerId, new DateTime(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc)));
        var request = new HttpRequestMessage(call.Method, "/api/me/drafts");
        request.Options.Set(DraftedInA, participant.Id);
        return request;
    }

    // For the caller's unfinished drafts: A's worker's list holds the draft that the call started in A for them, and
    // neither A's manager's nor B's manager's does (§5.9 item 7). Tests start other drafts for A's worker, so the
    // check looks for the call's own, never at the whole list.
    private static async Task<IReadOnlyList<string>> HoldsTheDraftStartedInAForAsWorkerAlone(
        HttpResponseMessage response, Caller caller, SeededOrganisations _)
    {
        if (response.RequestMessage?.Options.TryGetValue(DraftedInA, out var participantId) is not true)
        {
            throw new InvalidOperationException($"The request names no draft of A's in its {nameof(DraftedInA)}.");
        }

        var answer = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var listed = answer.EnumerateArray().Any(row => row.GetProperty("participantId").GetGuid() == participantId);
        return (caller, listed) switch
        {
            (Caller.WorkerOfA, false) => [$"lacks the caller's own draft of {participantId}"],
            (Caller.ManagerOfA, true) => [$"holds A's worker's draft of {participantId}"],
            (Caller.ManagerOfB, true) => [$"holds the other organisation's draft of {participantId}"],
            _ => [],
        };
    }

    private static Task<HttpRequestMessage> ANoteSubmittedInA(EndpointCall call) => ASubmittedNoteInA(call, "");

    // The same note's versions.
    private static Task<HttpRequestMessage> TheVersionsOfANoteSubmittedInA(EndpointCall call) =>
        ASubmittedNoteInA(call, "/versions");

    // The same note's version 1.
    private static Task<HttpRequestMessage> VersionOneOfANoteSubmittedInA(EndpointCall call) =>
        ASubmittedNoteInA(call, "/versions/1");

    // Today's note of a participant whom the request first adds to A, submitted by A's worker, as Submit leaves it, at
    // its address and the suffix. Its times are made up (A35).
    private static async Task<HttpRequestMessage> ASubmittedNoteInA(EndpointCall call, string addressSuffix)
    {
        var a = call.Seeded.A;
        var participant = await call.Services.AddParticipantAsync(a);
        var today = call.Services.GetRequiredService<MelbourneClock>().Today();
        await call.Services.AddSubmittedAsync(
            a,
            new(
                participant.Id,
                today,
                a.WorkerId,
                new DateTime(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 2, 6, 42, 0, DateTimeKind.Utc)));
        return new HttpRequestMessage(
            call.Method,
            string.Create(
                CultureInfo.InvariantCulture,
                $"/api/participants/{participant.Id}/notes/{today:yyyy-MM-dd}{addressSuffix}"));
    }

    // The daily report of 3 August 2026, as a Word file, asked for once the request has added to A a participant of its
    // own, because a participant has one live note a day, with a note submitted by A's worker, as Submit leaves it. No
    // test seeds a note of B's on that day. Its times are made up (A35).
    private static async Task<HttpRequestMessage> TheDailyReportOfANoteSubmittedInA(EndpointCall call)
    {
        var a = call.Seeded.A;
        var participant = await call.Services.AddParticipantAsync(a);
        await call.Services.AddSubmittedAsync(
            a,
            new(
                participant.Id,
                new DateOnly(2026, 8, 3),
                a.WorkerId,
                new DateTime(2026, 8, 3, 1, 14, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 3, 6, 42, 0, DateTimeKind.Utc)));
        return new HttpRequestMessage(call.Method, "/api/reports/daily/2026-08-03/export?format=docx");
    }

    // A's seeded participant's note form for today.
    private static Task<HttpRequestMessage> TodaysNoteFormOfTheSeededParticipantOfA(EndpointCall call) =>
        Task.FromResult(new HttpRequestMessage(call.Method, TodaysDraftPath(call, call.Seeded.A.ParticipantId)));

    // The first save of today's note of a participant whom the request first adds to A, with no goals, as the form
    // sends it: from a page that has not saved the note before, with the template's lists' version, and nothing ticked
    // or written yet.
    private static async Task<HttpRequestMessage> StartingTodaysNoteOfAParticipantOfItsOwn(EndpointCall call)
    {
        var participant = await call.Services.AddParticipantAsync(call.Seeded.A);
        return new HttpRequestMessage(call.Method, TodaysDraftPath(call, participant.Id))
        {
            Content = JsonContent.Create(new
            {
                clientId = Guid.NewGuid(),
                seq = 1,
                listsVersion = await ListsVersionOfTodaysFormAsync(call, participant.Id),
                narrative = "",
                tickedGoalIds = Array.Empty<Guid>(),
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
            }),
        };
    }

    // Submit of today's note of a participant whom the request first adds to A, a draft of A's worker's, as its first
    // save leaves it, with no goals, as the screen sends it: on base 0, with its text, and with a key of its own. The
    // draft's start time is made up (A35).
    private static async Task<HttpRequestMessage> SubmittingADraftOfTheWorkerOfA(EndpointCall call)
    {
        var a = call.Seeded.A;
        var participant = await call.Services.AddParticipantAsync(a);
        var today = call.Services.GetRequiredService<MelbourneClock>().Today();
        await call.Services.AddDraftAsync(
            a, new(participant.Id, today, a.WorkerId, new DateTime(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc)));
        var request = new HttpRequestMessage(
            call.Method,
            string.Create(
                CultureInfo.InvariantCulture, $"/api/participants/{participant.Id}/notes/{today:yyyy-MM-dd}/versions"))
        {
            Content = JsonContent.Create(new
            {
                baseVersion = 0,
                narrative = "Jane made toast.",
                tickedGoalIds = Array.Empty<Guid>(),
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged = false,
                flagReason = (string?)null,
            }),
        };
        request.Headers.Add(NoteVersionEndpoints.IdempotencyKeyHeader, Guid.NewGuid().ToString());
        return request;
    }

    // Discard draft of today's note of a participant whom the request first adds to A, a draft of A's worker's, as its
    // first save leaves it. The draft's start time is made up (A35).
    private static async Task<HttpRequestMessage> DiscardingADraftOfTheWorkerOfA(EndpointCall call)
    {
        var a = call.Seeded.A;
        var participant = await call.Services.AddParticipantAsync(a);
        var today = call.Services.GetRequiredService<MelbourneClock>().Today();
        await call.Services.AddDraftAsync(
            a, new(participant.Id, today, a.WorkerId, new DateTime(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc)));
        return new HttpRequestMessage(
            call.Method,
            string.Create(
                CultureInfo.InvariantCulture, $"/api/participants/{participant.Id}/notes/{today:yyyy-MM-dd}/discard"));
    }

    // Mark reviewed of a note that A's worker submitted flagged, as Submit leaves it, in To review at version 1, for a
    // participant whom the request first adds to A. The note's date, times and reason are made up (A35).
    private static async Task<HttpRequestMessage> ReviewingAFlaggedNoteOfTheWorkerOfA(EndpointCall call)
    {
        var a = call.Seeded.A;
        var participant = await call.Services.AddParticipantAsync(a);
        var noteDate = new DateOnly(2026, 10, 2);
        await call.Services.AddSubmittedAsync(
            a,
            new(
                participant.Id,
                noteDate,
                a.WorkerId,
                new DateTime(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 2, 6, 42, 0, DateTimeKind.Utc),
                IsFlagged: true,
                FlagReason: "Jane seemed unwell."));
        var path = string.Create(
            CultureInfo.InvariantCulture, $"/api/participants/{participant.Id}/notes/{noteDate:yyyy-MM-dd}/reviews");
        return new HttpRequestMessage(call.Method, path) { Content = JsonContent.Create(new { versionNumber = 1 }) };
    }

    // Cancel of today's note of a participant whom the request first adds to A, submitted by A's worker, with a pending
    // edit each for A's worker and A's manager, as their first saves of an edit leave them. The note's dates and times
    // are made up (A35).
    private static async Task<HttpRequestMessage> CancellingAPendingEditOfTheWorkerOfAAndTheManagerOfA(
        EndpointCall call)
    {
        var a = call.Seeded.A;
        var participant = await call.Services.AddParticipantAsync(a);
        var today = call.Services.GetRequiredService<MelbourneClock>().Today();
        var submittedAtUtc = new DateTime(2026, 10, 2, 6, 42, 0, DateTimeKind.Utc);
        var noteId = await call.Services.AddSubmittedAsync(
            a,
            new(
                participant.Id,
                today,
                a.WorkerId,
                new DateTime(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc),
                submittedAtUtc));
        await call.Services.AddPendingEditAsync(a, noteId, a.WorkerId, submittedAtUtc);
        await call.Services.AddPendingEditAsync(a, noteId, a.ManagerId, submittedAtUtc);
        return new HttpRequestMessage(call.Method, TodaysDraftPath(call, participant.Id));
    }

    // The To review list, asked for once the request has added to A a participant of its own, whom its FlaggedInA
    // option names, with a note submitted flagged by A's worker, as Submit leaves it. The note's date, times and reason
    // are made up (A35).
    private static async Task<HttpRequestMessage> ToReviewAfterANoteFlaggedInA(EndpointCall call)
    {
        var a = call.Seeded.A;
        var participant = await call.Services.AddParticipantAsync(a);
        await call.Services.AddSubmittedAsync(
            a,
            new(
                participant.Id,
                new DateOnly(2026, 10, 2),
                a.WorkerId,
                new DateTime(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 2, 6, 42, 0, DateTimeKind.Utc),
                IsFlagged: true,
                FlagReason: "Jane seemed unwell."));
        var request = new HttpRequestMessage(call.Method, "/api/reviews?status=toReview");
        request.Options.Set(FlaggedInA, participant.Id);
        return request;
    }

    // For the To review list: A's manager's list holds the note that the call flagged in A, and B's manager's does not
    // (§5.9 item 7). Tests flag other notes in A, so the check looks for the call's own, never at the whole list.
    private static async Task<IReadOnlyList<string>> HoldsTheNoteFlaggedInAForAsManagerAlone(
        HttpResponseMessage response, Caller caller, SeededOrganisations _)
    {
        if (response.RequestMessage?.Options.TryGetValue(FlaggedInA, out var participantId) is not true)
        {
            throw new InvalidOperationException($"The request names no note flagged in A in its {nameof(FlaggedInA)}.");
        }

        var answer = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var listed = answer.EnumerateArray().Any(row => row.GetProperty("participantId").GetGuid() == participantId);
        return (caller, listed) switch
        {
            (Caller.ManagerOfA, false) => [$"lacks the caller's own organisation's flagged note of {participantId}"],
            (Caller.ManagerOfB, true) => [$"holds the other organisation's flagged note of {participantId}"],
            _ => [],
        };
    }

    // The draft's path of the participant's note for today in Melbourne, by the app's own clock.
    // The lists' version that the participant's form opens with, as A's worker's page reads it from the app: A's lists
    // are not the seeded ones alone, since other tests add groups and items to A for good.
    private static async Task<string?> ListsVersionOfTodaysFormAsync(EndpointCall call, Guid participantId)
    {
        using var client = call.Services.GetRequiredService<IServer>() is TestServer server
            ? server.CreateClient()
            : throw new InvalidOperationException("The matrix calls an app that runs on the test server.");
        client.BaseAddress = new("https://localhost");
        client.SignInAs(call.Seeded.A.WorkerId);
        using var response = await client.GetAsync(
            TodaysDraftPath(call, participantId), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var form = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return form.GetProperty("listsVersion").GetString();
    }

    private static string TodaysDraftPath(EndpointCall call, Guid participantId)
    {
        var today = call.Services.GetRequiredService<MelbourneClock>().Today()
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"/api/participants/{participantId}/notes/{today}/draft";
    }

    // An invite of a worker with a made-up address (A35) that no other call or test uses.
    private static Task<HttpRequestMessage> AnInviteOfANewAddress(EndpointCall call) =>
        Task.FromResult(new HttpRequestMessage(call.Method, "/api/admin/users")
        {
            Content = JsonContent.Create(new
            {
                displayName = "Avery Brooks",
                email = $"avery.brooks.{Guid.NewGuid():N}@example.org",
                role = "Worker",
            }),
        });

    // For a change to a user, at /api/admin/users/{id}/{action}: a request for a worker whom it first invites into A,
    // sending the worker's ETag (WithETagOfANewInviteInA).
    private static Func<EndpointCall, Task<HttpRequestMessage>> OnANewInviteInA(string action) => call =>
        WithETagOfANewInviteInA(
            call, user => new HttpRequestMessage(call.Method, $"/api/admin/users/{user.Id}/{action}"));

    // For an edit of a user, at /api/admin/users/{id}: as for a change, a request with a body that gives the worker a
    // new name, so that the call changes them and sends nothing.
    private static Task<HttpRequestMessage> ARenameOfANewInviteInA(EndpointCall call) =>
        WithETagOfANewInviteInA(call, user => new HttpRequestMessage(call.Method, $"/api/admin/users/{user.Id}")
        {
            Content = JsonContent.Create(new { displayName = "Avery Morgan", email = user.Email, role = "Worker" }),
        });

    // The request that requestFor builds for a worker whom it first invites into A through the invite core, with an
    // address that no other call or test uses (A35), sending the worker's ETag, as the Users screen sends it (D78).
    private static async Task<HttpRequestMessage> WithETagOfANewInviteInA(
        EndpointCall call, Func<ApplicationUser, HttpRequestMessage> requestFor)
    {
        var a = call.Seeded.A;
        await using var scope = call.Services.CreateAsyncScope();
        var invite = await scope.ServiceProvider.GetRequiredService<Invitations>().InviteAsync(
            a.OrganisationId, "Avery Brooks", $"avery.brooks.{Guid.NewGuid():N}@example.org", UserRole.Worker,
            a.ManagerId);
        Assert.True(invite.Succeeded, string.Join(" ", invite.Errors.Select(error => error.Code)));

        var request = requestFor(invite.User);
        request.Headers.IfMatch.Add(new EntityTagHeaderValue(IfMatch.ETag(invite.User.ConcurrencyStamp!)));
        return request;
    }

    // A correction of a participant whom the request first adds to A, sent with their ETag as If-Match, so that each
    // call changes a participant of its own, as the seeded-rows rule asks (tests/README.md).
    private static async Task<HttpRequestMessage> CorrectingAParticipantOfItsOwn(EndpointCall call)
    {
        var participant = await call.Services.AddParticipantAsync(call.Seeded.A);
        var request = new HttpRequestMessage(call.Method, $"/api/admin/participants/{participant.Id}")
        {
            Content = JsonContent.Create(
                new { givenName = "Matrix", familyName = "Corrected", dateOfBirth = "1990-01-01" }),
        };
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(participant.ETag));
        return request;
    }

    // An archive or a restore, with no body, of a participant whom the request first adds to A, archived or not, sent
    // with their ETag as If-Match, so that each call changes a participant of its own (tests/README.md).
    private static async Task<HttpRequestMessage> ChangingTheStatusOfAParticipantOfItsOwn(
        EndpointCall call, string action, bool archived)
    {
        var participant = await call.Services.AddParticipantAsync(call.Seeded.A, archived);
        var request = new HttpRequestMessage(call.Method, $"/api/admin/participants/{participant.Id}/{action}");
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(participant.ETag));
        return request;
    }

    // A goal added to a participant whom the request first adds to A, so that each call adds to a participant of its
    // own, never the seeded one (tests/README.md).
    private static async Task<HttpRequestMessage> AddingAGoalToAParticipantOfItsOwn(EndpointCall call)
    {
        var participant = await call.Services.AddParticipantAsync(call.Seeded.A);
        return new HttpRequestMessage(call.Method, $"/api/admin/participants/{participant.Id}/goals")
        {
            Content = JsonContent.Create(new { text = "Makes own breakfast" }),
        };
    }

    // A reword of a goal that the request first adds to a participant whom it adds to A, sent with the goal's ETag as
    // If-Match, so that each call changes a goal of its own (tests/README.md).
    private static async Task<HttpRequestMessage> RewordingAGoalOfItsOwn(EndpointCall call)
    {
        var participant = await call.Services.AddParticipantAsync(call.Seeded.A);
        var goal = await call.Services.AddGoalAsync(call.Seeded.A, participant.Id, new("Makes own breakfast", 0));
        var request = new HttpRequestMessage(call.Method, $"/api/admin/goals/{goal.Id}")
        {
            Content = JsonContent.Create(new { text = "Makes own lunch" }),
        };
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(goal.ETag));
        return request;
    }

    // An archive or a restore, with no body, of a goal that the request first adds to a participant whom it adds to A,
    // archived or not, sent with the goal's ETag as If-Match, so that each call changes a goal of its own
    // (tests/README.md).
    private static async Task<HttpRequestMessage> ChangingThePlaceOfAGoalOfItsOwn(
        EndpointCall call, string action, bool archived)
    {
        var participant = await call.Services.AddParticipantAsync(call.Seeded.A);
        var goal = await call.Services.AddGoalAsync(
            call.Seeded.A,
            participant.Id,
            new("Makes own breakfast", 0, ArchivedAtUtc: archived ? TestGoals.AddedAtUtc : null));
        var request = new HttpRequestMessage(call.Method, $"/api/admin/goals/{goal.Id}/{action}");
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(goal.ETag));
        return request;
    }

    // A reorder of the active goals of a participant whom the request first adds to A with two active goals and an
    // archived one, sending the active ones' IDs the other way round, so that each call changes goals of its own
    // (tests/README.md).
    private static async Task<HttpRequestMessage> ReorderingTheGoalsOfAParticipantOfItsOwn(EndpointCall call)
    {
        var participant = await call.Services.AddParticipantAsync(call.Seeded.A);
        var goals = await call.Services.AddGoalsAsync(
            call.Seeded.A,
            participant.Id,
            new("Makes own breakfast", 0),
            new("Uses the washing machine", 1, ArchivedAtUtc: TestGoals.AddedAtUtc),
            new("Phones her sister", 2));
        return new HttpRequestMessage(call.Method, $"/api/admin/participants/{participant.Id}/goals/order")
        {
            Content = JsonContent.Create(new { goalIds = new[] { goals[2], goals[0] } }),
        };
    }

    // A rename of a group that the request first adds to A, sent with the group's ETag as If-Match, so that each call
    // changes a group of its own, never the seeded Every note (tests/README.md).
    private static async Task<HttpRequestMessage> RenamingAGroupOfItsOwn(EndpointCall call)
    {
        var group = await call.Services.AddGroupAsync(
            call.Seeded.A.OrganisationId, new TestCommonItemGroup("Community outing", 0));
        var request = new HttpRequestMessage(call.Method, $"/api/admin/common-item-groups/{group.Id}")
        {
            Content = JsonContent.Create(new { name = "Outings" }),
        };
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(group.ETag));
        return request;
    }

    // A reorder of A's active groups other than Every note, to which the request first adds two of its own, sending
    // every one of them in the list's order but those two, which change places, so that each call moves groups of its
    // own and leaves every other where it was (tests/README.md).
    private static async Task<HttpRequestMessage> ReorderingTheGroupsWithTwoOfItsOwn(EndpointCall call)
    {
        var organisationId = call.Seeded.A.OrganisationId;
        var own = await call.Services.AddGroupsAsync(
            organisationId,
            new TestCommonItemGroup("Community outing", 0),
            new TestCommonItemGroup("Personal care", 0));
        var groupIds = await call.Services.ActiveGroupIdsAsync(organisationId);
        var (first, second) = (groupIds.IndexOf(own[0]), groupIds.IndexOf(own[1]));
        (groupIds[first], groupIds[second]) = (groupIds[second], groupIds[first]);
        return new HttpRequestMessage(call.Method, "/api/admin/common-item-groups/order")
        {
            Content = JsonContent.Create(new { groupIds }),
        };
    }

    // An archive or a restore, with no body, of a group that the request first adds to A, archived or not, sent with
    // the group's ETag as If-Match, so that each call changes a group of its own, never the seeded Every note
    // (tests/README.md).
    private static async Task<HttpRequestMessage> ChangingTheStateOfAGroupOfItsOwn(
        EndpointCall call, string action, bool archived)
    {
        var group = await call.Services.AddGroupAsync(
            call.Seeded.A.OrganisationId,
            new TestCommonItemGroup(
                "Community outing", 0, ArchivedAtUtc: archived ? TestCommonItems.AddedAtUtc : null));
        var request = new HttpRequestMessage(call.Method, $"/api/admin/common-item-groups/{group.Id}/{action}");
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(group.ETag));
        return request;
    }

    // An item added to a group that the request first adds to A, so that each call adds to a group of its own, never
    // the seeded Every note (tests/README.md).
    private static async Task<HttpRequestMessage> AddingAnItemToAGroupOfItsOwn(EndpointCall call)
    {
        var groupId = Assert.Single(await call.Services.AddGroupsAsync(
            call.Seeded.A.OrganisationId, new TestCommonItemGroup("Community outing", 0)));
        return new HttpRequestMessage(call.Method, $"/api/admin/common-item-groups/{groupId}/items")
        {
            Content = JsonContent.Create(new { text = "Travelled by bus or train" }),
        };
    }

    // A reword of an item that the request first adds to a group that it adds to A, sent with the item's ETag as
    // If-Match, so that each call changes an item of its own, never one of the seeded Every note (tests/README.md).
    private static async Task<HttpRequestMessage> RewordingAnItemOfItsOwn(EndpointCall call)
    {
        var groupId = Assert.Single(await call.Services.AddGroupsAsync(
            call.Seeded.A.OrganisationId, new TestCommonItemGroup("Community outing", 0)));
        var item = await call.Services.AddItemAsync(
            call.Seeded.A.OrganisationId, groupId, new TestCommonItem("Travelled by bus or train", 0));
        var request = new HttpRequestMessage(call.Method, $"/api/admin/common-items/{item.Id}")
        {
            Content = JsonContent.Create(new { text = "Travelled by tram" }),
        };
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(item.ETag));
        return request;
    }

    // A reorder of the active items of a group that the request first adds to A with two active items and an archived
    // one, sending the active ones' IDs the other way round, so that each call changes items of its own, never those of
    // the seeded Every note (tests/README.md).
    private static async Task<HttpRequestMessage> ReorderingTheItemsOfAGroupOfItsOwn(EndpointCall call)
    {
        var groupId = Assert.Single(await call.Services.AddGroupsAsync(
            call.Seeded.A.OrganisationId, new TestCommonItemGroup("Community outing", 0)));
        var items = await call.Services.AddItemsAsync(
            call.Seeded.A.OrganisationId,
            groupId,
            new("Travelled by bus or train", 0),
            new("Packed a day bag", 1, ArchivedAtUtc: TestCommonItems.AddedAtUtc),
            new("Bought lunch", 2));
        return new HttpRequestMessage(call.Method, $"/api/admin/common-item-groups/{groupId}/items/order")
        {
            Content = JsonContent.Create(new { commonItemIds = new[] { items[2], items[0] } }),
        };
    }

    // An archive or a restore, with no body, of an item that the request first adds to a group that it adds to A,
    // archived or not, sent with the item's ETag as If-Match, so that each call changes an item of its own, never one
    // of the seeded Every note (tests/README.md).
    private static async Task<HttpRequestMessage> ChangingThePlaceOfAnItemOfItsOwn(
        EndpointCall call, string action, bool archived)
    {
        var groupId = Assert.Single(await call.Services.AddGroupsAsync(
            call.Seeded.A.OrganisationId, new TestCommonItemGroup("Community outing", 0)));
        var item = await call.Services.AddItemAsync(
            call.Seeded.A.OrganisationId,
            groupId,
            new TestCommonItem(
                "Travelled by bus or train", 0, ArchivedAtUtc: archived ? TestCommonItems.AddedAtUtc : null));
        var request = new HttpRequestMessage(call.Method, $"/api/admin/common-items/{item.Id}/{action}");
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(item.ETag));
        return request;
    }

    // A new authenticator's passkey, which no account holds. The endpoint looks for the account before anything reads
    // the challenge, so a made-up one does, with no options asked for first, and the call changes nothing.
    private static Task<HttpRequestMessage> WithAPasskeyNoAccountHolds(EndpointCall _)
    {
        using var authenticator = new TestAuthenticator();
        return Task.FromResult(PasskeySignIn.Request(
            authenticator.GetAssertion(Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)))));
    }

    // For an endpoint that lists an organisation's rows, such as its users: a JSON array of rows, each with its id, as
    // the answer or as its property `listedIn`, that holds every one of the caller's own organisation's seeded rows
    // that `seededIds` gives and none of the other's, each named in a difference as `what`. Tests add rows to the
    // seeded organisations, so it may hold others of the caller's own.
    private static Func<HttpResponseMessage, Caller, SeededOrganisations, Task<IReadOnlyList<string>>>
        HoldsTheCallersOwnSeeded(string what, Func<SeededOrganisation, Guid[]> seededIds, string? listedIn = null) =>
        async (response, caller, seeded) =>
        {
            var (own, other) = caller switch
            {
                Caller.WorkerOfA or Caller.ManagerOfA => (seeded.A, seeded.B),
                Caller.ManagerOfB => (seeded.B, seeded.A),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(caller), caller, $"A signed-out caller has no {what}s."),
            };
            var answer = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            var rows = listedIn is null ? answer : answer.GetProperty(listedIn);
            var ids = rows.EnumerateArray().Select(row => row.GetProperty("id").GetGuid()).ToHashSet();
            return
            [
                .. seededIds(own).Where(id => !ids.Contains(id))
                    .Select(id => $"lacks the caller's own organisation's {what} {id}"),
                .. seededIds(other).Where(ids.Contains).Select(id => $"holds the other organisation's {what} {id}"),
            ];
        };
}

/// <summary>
/// A row of the <see cref="EndpointMatrix"/>: an endpoint, found by the <paramref name="Methods"/> it names, in any
/// order, or <see cref="EndpointMatrix.AnyMethod"/>, and its <paramref name="Route"/> pattern as mapped; the status
/// each <see cref="Caller"/> gets from it; and how to call it.
/// </summary>
/// <remarks>
/// <paramref name="Request"/> builds the request for one call. Left out, the request goes to the route pattern, which
/// must then have no parameters, so a row whose route has them builds a request to a path its endpoint answers. It is
/// asked again for each call, and may first add the record that a call acts on: a call commits, so a row whose calls
/// change something gives each call a record of its own (tests/README.md).
/// <paramref name="Answer"/> checks the answer to a call that gets the row's status when that is a success, given the
/// caller and the seeded organisations, and lists how it differs from what the row expects; it is for an endpoint
/// that lists an organisation's rows, whose list must hold none of the other organisation's (design.md §5.9 item 7).
/// </remarks>
internal sealed record EndpointRow(
    IReadOnlyList<string> Methods,
    string Route,
    HttpStatusCode SignedOut,
    HttpStatusCode WorkerOfA,
    HttpStatusCode ManagerOfA,
    HttpStatusCode ManagerOfB,
    Func<EndpointCall, Task<HttpRequestMessage>>? Request = null,
    Func<HttpResponseMessage, Caller, SeededOrganisations, Task<IReadOnlyList<string>>>? Answer = null)
{
    /// <summary>The status that <paramref name="caller"/> gets from the endpoint.</summary>
    public HttpStatusCode StatusFor(Caller caller) => caller switch
    {
        Caller.SignedOut => SignedOut,
        Caller.WorkerOfA => WorkerOfA,
        Caller.ManagerOfA => ManagerOfA,
        Caller.ManagerOfB => ManagerOfB,
        _ => throw new ArgumentOutOfRangeException(nameof(caller), caller, null),
    };
}

/// <summary>
/// A call of an endpoint with one of the methods that <see cref="EndpointMatrix.CallsTo"/> gives it, with the services
/// of the app that maps the endpoint and the seeded organisations, for a row's builder that adds the record the call
/// acts on.
/// </summary>
internal sealed record EndpointCall(
    HttpMethod Method, RouteEndpoint Endpoint, IServiceProvider Services, SeededOrganisations Seeded)
{
    // The methods that antiforgery leaves unchecked.
    private static readonly HttpMethod[] Safe = [HttpMethod.Get, HttpMethod.Head, HttpMethod.Options, HttpMethod.Trace];

    /// <summary>
    /// Whether the call changes something, so it must send the caller's antiforgery token, and any body as JSON
    /// (design.md §9.8): a call of an endpoint that names its methods, with any method but GET, HEAD, OPTIONS and
    /// TRACE, such as POST, PUT or DELETE. An endpoint that names none, such as a catch-all, answers those methods too,
    /// but changes nothing.
    /// </summary>
    public bool ChangesState => EndpointMatrix.MethodsOf(Endpoint) is not [] && !Safe.Contains(Method);

    /// <summary>
    /// The request for this call, as the endpoint's row builds it. An endpoint with no row, such as one that a test
    /// maps for itself, is called at its route pattern.
    /// </summary>
    public Task<HttpRequestMessage> RequestAsync() =>
        (EndpointMatrix.RowOf(Endpoint)?.Request ?? AtRoutePattern)(this);

    // MapStaticAssets' patterns have no leading slash.
    private static Task<HttpRequestMessage> AtRoutePattern(EndpointCall call) =>
        call.Endpoint.RoutePattern is { Parameters: [], RawText: { } pattern }
            ? Task.FromResult(new HttpRequestMessage(call.Method, "/" + pattern.TrimStart('/')))
            : throw new NotSupportedException(
                $"{call.Endpoint.RoutePattern.RawText} has route parameters, so its row builds the request.");
}
