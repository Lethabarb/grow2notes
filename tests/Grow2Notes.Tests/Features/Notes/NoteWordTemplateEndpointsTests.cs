using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Notes;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// The Word-template fields of a note (design.md §5.7, §5.8, §6.3, D104), through <c>GET</c> and
/// <c>PUT {base}/draft</c>, <c>POST {base}/versions</c> and the two reads, called with the test-only sign-in
/// (<see cref="TestSignIn"/>): the
/// three attendance ticks; each goal's term; the Medication text of a note that has a Medication box, which a
/// participant's switch gives it when it is started and which it keeps whatever the switch says later; and the details
/// of a ticked common item that asks for them, which Submit and Save changes require but a draft does not. A change to
/// the term, the need for details or the switch since the form opened gives <c>409 note.lists_changed</c> at the first
/// save. The tests add their own participants, goals and, to the lists of A, items and a group (tests/README.md). The
/// other rules of the draft and the versions are in <see cref="NoteDraftEndpointsTests"/> and
/// <see cref="NoteVersionEndpointsTests"/>.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NoteWordTemplateEndpointsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string EnterDetails = "Enter the details for each ticked item that asks for them.";

    private static readonly DateOnly OldDay = new(2026, 10, 2);

    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = new(2026, 10, 2, 5, 42, 0, DateTimeKind.Utc);

    public static TheoryData<string> SetUpChanges { get; } =
        ["a goal's term", "an item's need for details", "the switch"];

    // Each detail that a save refuses under commonItemDetails: for an item that does not ask, for an unknown item and
    // for text of 201 characters once cleaned.
    public static TheoryData<string> RefusedDetails { get; } =
        ["an item that does not ask", "an unknown item", "too long"];

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    private DateOnly Today => factory.Services.GetRequiredService<MelbourneClock>().Today();

    [Fact]
    public async Task The_template_carries_each_goal_s_term_and_each_item_s_need_for_details()
    {
        var scene = await AddSceneAsync();
        using var client = ClientFor(Caller.WorkerOfA);

        var form = await ReadFormAsync(client, scene.ParticipantId, Today);

        Assert.Equal(
            [(scene.ShortGoal, false), (scene.LongGoal, true)],
            form.GetProperty("goals").EnumerateArray().Select(
                goal => (goal.GetProperty("id").GetGuid(), goal.GetProperty("isMediumOrLongTerm").GetBoolean())));
        Assert.True(ItemOf(form, scene.Other).GetProperty("needsDetail").GetBoolean());
        Assert.False(ItemOf(form, scene.Plain).GetProperty("needsDetail").GetBoolean());
        Assert.True(ItemOf(form, scene.GroupOther).GetProperty("needsDetail").GetBoolean());
        Assert.All(new[] { scene.Other, scene.Plain, scene.GroupOther }, id =>
            Assert.Equal(JsonValueKind.Null, ItemOf(form, id).GetProperty("detail").ValueKind));
        Assert.Equal(
            (false, false, false), (
                form.GetProperty("isNotIn").GetBoolean(),
                form.GetProperty("isLateNotice").GetBoolean(),
                form.GetProperty("isSick").GetBoolean()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_template_s_medication_is_empty_with_the_participant_s_switch_on_and_null_with_it_off(
        bool hasMedicationLine)
    {
        var scene = await AddSceneAsync(hasMedicationLine);
        using var client = ClientFor(Caller.WorkerOfA);

        var form = await ReadFormAsync(client, scene.ParticipantId, Today);

        var medication = form.GetProperty("medication");
        Assert.Equal(hasMedicationLine ? JsonValueKind.String : JsonValueKind.Null, medication.ValueKind);
        Assert.Equal(hasMedicationLine ? "" : null, medication.GetString());
    }

    [Theory]
    [MemberData(nameof(SetUpChanges))]
    public async Task A_change_to_the_set_up_between_the_form_and_the_first_save_gives_409_note_lists_changed(
        string change)
    {
        var scene = await AddSceneAsync();
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);
        await ChangeSetUpAsync(scene, change);

        using var response = await PutAsync(
            client, scene.ParticipantId, Save(Guid.NewGuid(), 1, ListsVersionOf(form)), Today);

        await ReadCodeAsync(response, HttpStatusCode.Conflict, "note.lists_changed");
        Assert.Empty(await NotesAsync(scene.ParticipantId));
    }

    [Fact]
    public async Task Attendance_medication_and_details_save_and_read_back()
    {
        var scene = await AddSceneAsync(hasMedicationLine: true);
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);

        using var started = await PutAsync(
            client,
            scene.ParticipantId,
            Save(Guid.NewGuid(), 1, ListsVersionOf(form), body =>
            {
                body["isNotIn"] = true;
                body["isSick"] = true;
                body["medication"] = "Panadol at 9 am.\nNothing else.";
                body["tickedGoalIds"] = Ids(scene.LongGoal);
                body["pickedGroupIds"] = Ids(scene.Group);
                body["tickedCommonItemIds"] = Ids(scene.Other, scene.GroupOther);
                body["commonItemDetails"] = Details((scene.Other, "Zoë’s walk"), (scene.GroupOther, "Tram 86"));
            }),
            Today);

        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        var saved = await ReadFormAsync(client, scene.ParticipantId, Today);
        Assert.Equal(
            (true, false, true, "Panadol at 9 am.\nNothing else."),
            (saved.GetProperty("isNotIn").GetBoolean(), saved.GetProperty("isLateNotice").GetBoolean(),
                saved.GetProperty("isSick").GetBoolean(), saved.GetProperty("medication").GetString()));
        Assert.Equal("Zoë’s walk", ItemOf(saved, scene.Other).GetProperty("detail").GetString());
        Assert.Equal("Tram 86", ItemOf(saved, scene.GroupOther).GetProperty("detail").GetString());
        Assert.Equal(JsonValueKind.Null, ItemOf(saved, scene.Plain).GetProperty("detail").ValueKind);
        var draft = await StoredDraftAsync(scene.ParticipantId);
        Assert.Equal(("Panadol at 9 am.\nNothing else.", true, false, true),
            (draft.Medication, draft.IsNotIn, draft.IsLateNotice, draft.IsSick));
    }

    // A line break becomes a space and the ends are trimmed, as a row's text is; 200 characters remain, so it is kept.
    [Fact]
    public async Task A_detail_is_stored_with_line_breaks_as_spaces_and_a_blank_one_as_none()
    {
        var scene = await AddSceneAsync();
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);
        var clientId = Guid.NewGuid();
        var long200 = $"  {new string('a', 100)} \r\n\n {new string('b', 99)}\t";

        using var started = await PutAsync(
            client,
            scene.ParticipantId,
            Save(clientId, 1, ListsVersionOf(form), body =>
            {
                body["pickedGroupIds"] = Ids(scene.Group);
                body["tickedCommonItemIds"] = Ids(scene.Other, scene.GroupOther);
                body["commonItemDetails"] = Details((scene.Other, long200), (scene.GroupOther, " \r\n "));
            }),
            Today);

        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        var saved = await ReadFormAsync(client, scene.ParticipantId, Today);
        Assert.Equal(
            new string('a', 100) + " " + new string('b', 99),
            ItemOf(saved, scene.Other).GetProperty("detail").GetString());
        Assert.Equal(JsonValueKind.Null, ItemOf(saved, scene.GroupOther).GetProperty("detail").ValueKind);
    }

    // A blank detail on a ticked item is no fault in a draft: the writer may tick before writing (A151).
    [Fact]
    public async Task A_blank_detail_on_a_ticked_item_is_accepted_in_a_draft()
    {
        var scene = await AddSceneAsync();
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);

        using var started = await PutAsync(
            client,
            scene.ParticipantId,
            Save(Guid.NewGuid(), 1, ListsVersionOf(form), body => body["tickedCommonItemIds"] = Ids(scene.Other)),
            Today);

        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        var saved = await ReadFormAsync(client, scene.ParticipantId, Today);
        Assert.True(ItemOf(saved, scene.Other).GetProperty("isTicked").GetBoolean());
        Assert.Equal(JsonValueKind.Null, ItemOf(saved, scene.Other).GetProperty("detail").ValueKind);
    }

    [Fact]
    public async Task A_detail_sent_for_an_item_that_is_not_ticked_is_dropped_not_refused()
    {
        var scene = await AddSceneAsync();
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);

        using var started = await PutAsync(
            client,
            scene.ParticipantId,
            Save(Guid.NewGuid(), 1, ListsVersionOf(form), body =>
                body["commonItemDetails"] = Details((scene.Other, "Walked"))),
            Today);

        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        var saved = await ReadFormAsync(client, scene.ParticipantId, Today);
        Assert.False(ItemOf(saved, scene.Other).GetProperty("isTicked").GetBoolean());
        Assert.Equal(JsonValueKind.Null, ItemOf(saved, scene.Other).GetProperty("detail").ValueKind);
    }

    // Unticking an item, and unpicking its group, clear its detail, so ticking it again starts with none.
    [Fact]
    public async Task Unticking_an_item_or_unpicking_its_group_clears_its_detail()
    {
        var scene = await AddSceneAsync();
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);
        var clientId = Guid.NewGuid();
        using (var started = await PutAsync(
            client,
            scene.ParticipantId,
            Save(clientId, 1, ListsVersionOf(form), body =>
            {
                body["pickedGroupIds"] = Ids(scene.Group);
                body["tickedCommonItemIds"] = Ids(scene.Other, scene.GroupOther);
                body["commonItemDetails"] = Details((scene.Other, "Walked"), (scene.GroupOther, "Tram 86"));
            }),
            Today))
        {
            Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        }

        // Other unticked, and the group unpicked with its item unticked, the details sent as they were.
        using (var cleared = await PutAsync(
            client,
            scene.ParticipantId,
            Save(clientId, 2, null, body =>
                body["commonItemDetails"] = Details((scene.Other, "Walked"), (scene.GroupOther, "Tram 86"))),
            Today))
        {
            Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        }

        // Ticked again with no detail.
        using (var again = await PutAsync(
            client,
            scene.ParticipantId,
            Save(clientId, 3, null, body => body["tickedCommonItemIds"] = Ids(scene.Other)),
            Today))
        {
            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        }

        var saved = await ReadFormAsync(client, scene.ParticipantId, Today);
        Assert.True(ItemOf(saved, scene.Other).GetProperty("isTicked").GetBoolean());
        Assert.Equal(JsonValueKind.Null, ItemOf(saved, scene.Other).GetProperty("detail").ValueKind);
        Assert.Equal(JsonValueKind.Null, ItemOf(saved, scene.GroupOther).GetProperty("detail").ValueKind);
    }

    [Theory]
    [MemberData(nameof(RefusedDetails))]
    public async Task A_detail_for_an_item_that_does_not_ask_or_that_is_too_long_gives_422_commonItemDetails(
        string kind)
    {
        var scene = await AddSceneAsync();
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);
        var details = kind switch
        {
            "an item that does not ask" => Details((scene.Plain, "Walked")),
            "an unknown item" => Details((Guid.NewGuid(), "Walked")),
            _ => Details((scene.Other, new string('a', Limits.ItemDetail + 1))),
        };

        using var response = await PutAsync(
            client,
            scene.ParticipantId,
            Save(Guid.NewGuid(), 1, ListsVersionOf(form), body =>
            {
                body["tickedCommonItemIds"] = Ids(scene.Other);
                body["commonItemDetails"] = details;
            }),
            Today);

        await ReadValidationProblemAsync(response, "commonItemDetails");
        Assert.Empty(await NotesAsync(scene.ParticipantId));
    }

    [Fact]
    public async Task Medication_on_a_note_with_no_Medication_box_gives_422_medication()
    {
        var scene = await AddSceneAsync(hasMedicationLine: false);
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);

        using var response = await PutAsync(
            client,
            scene.ParticipantId,
            Save(Guid.NewGuid(), 1, ListsVersionOf(form), body => body["medication"] = "Panadol"),
            Today);

        await ReadValidationProblemAsync(response, "medication");
        Assert.Empty(await NotesAsync(scene.ParticipantId));
    }

    [Fact]
    public async Task Medication_blank_without_the_box_is_no_fault_and_the_note_has_no_box()
    {
        var scene = await AddSceneAsync(hasMedicationLine: false);
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);

        using var response = await PutAsync(
            client,
            scene.ParticipantId,
            Save(Guid.NewGuid(), 1, ListsVersionOf(form), body => body["medication"] = "  "),
            Today);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null((await StoredDraftAsync(scene.ParticipantId)).Medication);
    }

    [Theory]
    [InlineData(1_000, HttpStatusCode.Created)]
    [InlineData(1_001, HttpStatusCode.UnprocessableEntity)]
    public async Task Medication_of_1000_characters_is_saved_and_of_1001_gives_422_medication(
        int length, HttpStatusCode status)
    {
        var scene = await AddSceneAsync(hasMedicationLine: true);
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);

        using var response = await PutAsync(
            client,
            scene.ParticipantId,
            Save(Guid.NewGuid(), 1, ListsVersionOf(form), body => body["medication"] = new string('m', length)),
            Today);

        Assert.Equal(status, response.StatusCode);
        if (status == HttpStatusCode.UnprocessableEntity)
        {
            await ReadValidationProblemAsync(response, "medication");
        }
    }

    [Fact]
    public async Task A_null_or_blank_medication_on_a_note_with_the_box_is_stored_as_empty()
    {
        var scene = await AddSceneAsync(hasMedicationLine: true);
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);
        var clientId = Guid.NewGuid();

        using (var started = await PutAsync(
            client,
            scene.ParticipantId,
            Save(clientId, 1, ListsVersionOf(form), body => body["medication"] = null),
            Today))
        {
            Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        }

        Assert.Equal("", (await StoredDraftAsync(scene.ParticipantId)).Medication);

        using (var written = await PutAsync(
            client,
            scene.ParticipantId,
            Save(clientId, 2, null, body => body["medication"] = "Panadol"),
            Today))
        {
            Assert.Equal(HttpStatusCode.OK, written.StatusCode);
        }

        using (var blank = await PutAsync(
            client, scene.ParticipantId, Save(clientId, 3, null, body => body["medication"] = " \n "), Today))
        {
            Assert.Equal(HttpStatusCode.OK, blank.StatusCode);
        }

        Assert.Equal("", (await StoredDraftAsync(scene.ParticipantId)).Medication);
    }

    // The body is the whole working copy (A153), so a body from a page that knows nothing of the new members unticks
    // the attendance, drops the details and writes no Medication text, but the box itself, which the working copy has,
    // stays.
    [Fact]
    public async Task A_body_of_the_old_shape_saves_with_nothing_ticked_and_the_Medication_box_kept()
    {
        var scene = await AddSceneAsync(hasMedicationLine: true);
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);
        var clientId = Guid.NewGuid();
        using (var started = await PutAsync(
            client,
            scene.ParticipantId,
            Save(clientId, 1, ListsVersionOf(form), body =>
            {
                body["isLateNotice"] = true;
                body["medication"] = "Panadol";
                body["tickedCommonItemIds"] = Ids(scene.Other);
                body["commonItemDetails"] = Details((scene.Other, "Walked"));
            }),
            Today))
        {
            Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        }

        var oldShape = new JsonObject
        {
            ["clientId"] = clientId,
            ["seq"] = 2,
            ["narrative"] = "Jane made toast.",
            ["tickedGoalIds"] = Ids(),
            ["pickedGroupIds"] = Ids(),
            ["tickedCommonItemIds"] = Ids(scene.Other),
            ["isFlagged"] = false,
            ["flagReason"] = null,
        };
        using (var saved = await PutAsync(client, scene.ParticipantId, oldShape, Today))
        {
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        }

        var draft = await StoredDraftAsync(scene.ParticipantId);
        Assert.Equal((false, ""), (draft.IsLateNotice, draft.Medication));
        Assert.Null(draft.Items.Single(item => item.SourceId == scene.Other).Detail);
    }

    [Fact]
    public async Task Submit_with_a_ticked_item_that_asks_for_details_and_none_gives_422_commonItemDetails()
    {
        var scene = await AddSceneAsync();
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);
        await StartAsync(client, scene, form, body => body["tickedCommonItemIds"] = Ids(scene.Other));

        using var response = await SubmitAsync(client, scene.ParticipantId, Content(
            body => body["tickedCommonItemIds"] = Ids(scene.Other)));

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        var errors = problem.RootElement.GetProperty("errors");
        Assert.Equal(["commonItemDetails"], errors.EnumerateObject().Select(error => error.Name));
        Assert.Equal(EnterDetails, errors.GetProperty("commonItemDetails")[0].GetString());
        Assert.Empty(await VersionsAsync(scene.ParticipantId));
    }

    [Fact]
    public async Task Submit_stores_attendance_medication_terms_and_details_on_version_1()
    {
        var scene = await AddSceneAsync(hasMedicationLine: true);
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);
        void Fill(JsonObject body)
        {
            body["isNotIn"] = true;
            body["isLateNotice"] = true;
            body["medication"] = "Panadol at 9 am.";
            body["tickedGoalIds"] = Ids(scene.LongGoal);
            body["pickedGroupIds"] = Ids(scene.Group);
            body["tickedCommonItemIds"] = Ids(scene.Other, scene.GroupOther, scene.Plain);
            body["commonItemDetails"] = Details((scene.Other, "Walked"), (scene.GroupOther, "Tram 86"));
        }

        await StartAsync(client, scene, form, Fill);
        using var response = await SubmitAsync(client, scene.ParticipantId, Content(Fill));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var version = Assert.Single(await VersionsAsync(scene.ParticipantId));
        Assert.Equal(
            (true, true, false, "Panadol at 9 am."),
            (version.IsNotIn, version.IsLateNotice, version.IsSick, version.Medication));
        var items = await VersionItemsAsync(version.Id);
        Assert.Equal(
            [(scene.ShortGoal, false), (scene.LongGoal, true)],
            items.Where(item => item.Section == NoteSection.Goal).OrderBy(item => item.Position)
                .Select(item => (item.GoalId!.Value, item.IsMediumOrLongTerm)));
        var common = items.Where(item => item.Section == NoteSection.CommonItem)
            .ToDictionary(item => item.CommonItemId!.Value);
        Assert.Equal((true, "Walked"), (common[scene.Other].NeedsDetail, common[scene.Other].Detail));
        Assert.Equal((true, "Tram 86"), (common[scene.GroupOther].NeedsDetail, common[scene.GroupOther].Detail));
        Assert.Equal((false, null), (common[scene.Plain].NeedsDetail, common[scene.Plain].Detail));
        Assert.All(items.Where(item => item.Section == NoteSection.Goal), item => Assert.False(item.NeedsDetail));
        Assert.All(common.Values, item => Assert.False(item.IsMediumOrLongTerm));
    }

    // R-17: attendance changes nothing about what a note needs, so a Not In note still needs its text.
    [Fact]
    public async Task A_note_ticked_Not_In_with_no_text_is_still_refused()
    {
        var scene = await AddSceneAsync();
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);
        await StartAsync(client, scene, form, body => body["isNotIn"] = true);

        using var response = await SubmitAsync(client, scene.ParticipantId, Content(body =>
        {
            body["isNotIn"] = true;
            body["narrative"] = "   ";
        }));

        await ReadValidationProblemAsync(response, "narrative");
        Assert.Empty(await VersionsAsync(scene.ParticipantId));
    }

    // Save changes asks for the details as Submit does, and does not refuse content that is unchanged (A69).
    [Fact]
    public async Task Save_changes_needs_the_details_of_a_ticked_item_and_unchanged_content_is_not_refused()
    {
        var scene = await AddSceneAsync();
        using var worker = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(worker, scene.ParticipantId, Today);
        void Fill(JsonObject body)
        {
            body["tickedCommonItemIds"] = Ids(scene.Other);
            body["commonItemDetails"] = Details((scene.Other, "Walked"));
        }

        await StartAsync(worker, scene, form, Fill);
        using (var submitted = await SubmitAsync(worker, scene.ParticipantId, Content(Fill)))
        {
            Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        }

        using var manager = ClientFor(Caller.ManagerOfA);
        var edit = Guid.NewGuid();
        using (var begun = await PutAsync(
            manager, scene.ParticipantId, EditContent(edit, 1, Fill), Today))
        {
            Assert.Equal(HttpStatusCode.OK, begun.StatusCode);
        }

        using (var unchanged = await SubmitAsync(manager, scene.ParticipantId, EditVersion(Fill)))
        {
            Assert.Equal(HttpStatusCode.Created, unchanged.StatusCode);
        }

        // Version 2 is the current one now: the detail is cleared in a pending edit on it, and kept ticked.
        void Cleared(JsonObject body) => body["tickedCommonItemIds"] = Ids(scene.Other);
        using (var again = await PutAsync(
            manager, scene.ParticipantId, EditContent(Guid.NewGuid(), 1, Cleared, baseVersion: 2), Today))
        {
            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        }

        using var refused = await SubmitAsync(manager, scene.ParticipantId, EditVersion(Cleared, baseVersion: 2));

        using var problem = JsonDocument.Parse(await refused.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal(
            ["commonItemDetails"], problem.RootElement.GetProperty("errors").EnumerateObject().Select(e => e.Name));
        Assert.Equal(2, (await VersionsAsync(scene.ParticipantId)).Count);
    }

    // A pending edit keeps the version's own Medication box, terms and need for details whatever the set-up says now
    // (A3), and a version saved before T44 has no box, so Medication text for it is refused.
    [Fact]
    public async Task A_pending_edit_keeps_the_version_s_box_terms_and_flags_after_the_set_up_changes()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A, hasMedicationLine: false);
        var goalId = Assert.Single(await factory.Services.AddGoalsAsync(
            seeded.A, participant.Id, new TestGoal("Plans a holiday", 0)));
        var itemId = Assert.Single(await factory.Services.AddItemsAsync(
            seeded.A.OrganisationId, seeded.A.EveryNoteGroupId, new TestCommonItem("Other (said at the time)", 0)));
        await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id,
            OldDay,
            seeded.A.WorkerId,
            StartedAtUtc,
            SubmittedAtUtc,
            Goals: [new(goalId, "Plans a holiday", false, IsMediumOrLongTerm: true)],
            Groups:
            [
                new(seeded.A.EveryNoteGroupId, "Every note", true, false, [
                    new(itemId, "Other (said at the time)", true, NeedsDetail: true, Detail: "Walked"),
                ]),
            ],
            Medication: "Panadol at 9 am."));
        using var manager = ClientFor(Caller.ManagerOfA);

        var form = await ReadFormAsync(manager, participant.Id, OldDay);

        Assert.Equal("Panadol at 9 am.", form.GetProperty("medication").GetString());
        Assert.True(form.GetProperty("goals")[0].GetProperty("isMediumOrLongTerm").GetBoolean());
        var item = ItemOf(form, itemId);
        Assert.True(item.GetProperty("needsDetail").GetBoolean());
        Assert.Equal("Walked", item.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_pending_edit_of_a_version_saved_before_T44_has_no_Medication_box_though_the_switch_is_on()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A, hasMedicationLine: true);
        await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id, OldDay, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc));
        using var manager = ClientFor(Caller.ManagerOfA);

        var form = await ReadFormAsync(manager, participant.Id, OldDay);

        Assert.Equal(JsonValueKind.Null, form.GetProperty("medication").ValueKind);
        using var response = await PutAsync(
            manager,
            participant.Id,
            EditContent(Guid.NewGuid(), 1, body => body["medication"] = "Panadol"),
            OldDay);
        await ReadValidationProblemAsync(response, "medication");
    }

    [Fact]
    public async Task The_read_and_the_version_read_give_the_new_fields_with_the_picked_groups_only()
    {
        var scene = await AddSceneAsync(hasMedicationLine: true);
        var unpicked = await AddUnpickedGroupAsync();
        using var client = ClientFor(Caller.WorkerOfA);
        var form = await ReadFormAsync(client, scene.ParticipantId, Today);
        void Fill(JsonObject body)
        {
            body["isSick"] = true;
            body["medication"] = "Panadol";
            body["tickedGoalIds"] = Ids(scene.LongGoal);
            body["pickedGroupIds"] = Ids(scene.Group);
            body["tickedCommonItemIds"] = Ids(scene.GroupOther);
            body["commonItemDetails"] = Details((scene.GroupOther, "Tram 86"));
        }

        await StartAsync(client, scene, form, Fill);
        using (var submitted = await SubmitAsync(client, scene.ParticipantId, Content(Fill)))
        {
            Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        }

        var read = await client.GetFromJsonAsync<JsonElement>(
            NotePath(scene.ParticipantId, Today), TestContext.Current.CancellationToken);
        using var manager = ClientFor(Caller.ManagerOfA);
        var version = await manager.GetFromJsonAsync<JsonElement>(
            NotePath(scene.ParticipantId, Today) + "/versions/1", TestContext.Current.CancellationToken);

        foreach (var current in new[] { read.GetProperty("current"), version })
        {
            Assert.Equal(
                (false, false, true, "Panadol"),
                (current.GetProperty("isNotIn").GetBoolean(), current.GetProperty("isLateNotice").GetBoolean(),
                    current.GetProperty("isSick").GetBoolean(), current.GetProperty("medication").GetString()));
            Assert.Equal(
                [("Makes own breakfast", false, false), ("Plans a holiday", true, true)],
                current.GetProperty("goals").EnumerateArray().Select(goal => (
                    goal.GetProperty("text").GetString(), goal.GetProperty("isTicked").GetBoolean(),
                    goal.GetProperty("isMediumOrLongTerm").GetBoolean())));
            var names = current.GetProperty("commonItemGroups").EnumerateArray()
                .Select(group => group.GetProperty("name").GetString()).ToList();
            Assert.Contains(scene.GroupName, names);
            Assert.DoesNotContain(unpicked, names);
            var picked = current.GetProperty("commonItemGroups").EnumerateArray()
                .Single(group => group.GetProperty("name").GetString() == scene.GroupName);
            Assert.Equal(
                ["text", "isTicked", "detail"],
                picked.GetProperty("items")[0].EnumerateObject().Select(property => property.Name));
            Assert.Equal("Tram 86", picked.GetProperty("items")[0].GetProperty("detail").GetString());
        }
    }

    // What a version saved before T44 holds: the column defaults, which read as no ticks, Short term goals, no
    // Medication box and no details.
    [Fact]
    public async Task A_version_with_the_defaults_reads_as_no_ticks_Short_term_no_medication_and_no_details()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A, hasMedicationLine: true);
        var goalId = Assert.Single(await factory.Services.AddGoalsAsync(
            seeded.A, participant.Id, new TestGoal("Plans a holiday", 0, IsMediumOrLongTerm: true)));
        var itemId = Assert.Single(await factory.Services.AddItemsAsync(
            seeded.A.OrganisationId,
            seeded.A.EveryNoteGroupId,
            new TestCommonItem("Other (said)", 0, NeedsDetail: true)));
        await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id,
            OldDay,
            seeded.A.WorkerId,
            StartedAtUtc,
            SubmittedAtUtc,
            Goals: [new(goalId, "Plans a holiday", true)],
            Groups: [new(seeded.A.EveryNoteGroupId, "Every note", true, false, [new(itemId, "Other (said)", true)])]));
        using var client = ClientFor(Caller.ManagerOfA);

        var version = await client.GetFromJsonAsync<JsonElement>(
            NotePath(participant.Id, OldDay) + "/versions/1", TestContext.Current.CancellationToken);

        Assert.Equal(
            (false, false, false, JsonValueKind.Null),
            (version.GetProperty("isNotIn").GetBoolean(), version.GetProperty("isLateNotice").GetBoolean(),
                version.GetProperty("isSick").GetBoolean(), version.GetProperty("medication").ValueKind));
        Assert.False(version.GetProperty("goals")[0].GetProperty("isMediumOrLongTerm").GetBoolean());
        var item = version.GetProperty("commonItemGroups")[0].GetProperty("items")[0];
        Assert.False(item.TryGetProperty("detail", out _));
    }

    private static string DraftPath(Guid participantId, DateOnly noteDate) =>
        string.Create(
            CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{noteDate:yyyy-MM-dd}/draft");

    private static string NotePath(Guid participantId, DateOnly noteDate) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{noteDate:yyyy-MM-dd}");

    private static JsonArray Ids(params Guid[] ids) => new([.. ids.Select(id => (JsonNode?)JsonValue.Create(id))]);

    private static JsonObject Details(params (Guid Id, string Text)[] entries)
    {
        var details = new JsonObject();
        foreach (var (id, text) in entries)
        {
            details[id.ToString()] = text;
        }

        return details;
    }

    // A draft save's body: from a page, with its number, whole, with nothing ticked and nothing written unless
    // <paramref name="fill"/> says so.
    private static JsonObject Save(Guid clientId, int seq, string? listsVersion, Action<JsonObject>? fill = null)
    {
        var body = new JsonObject
        {
            ["clientId"] = clientId,
            ["seq"] = seq,
            ["listsVersion"] = listsVersion,
        };
        Content(fill, body);
        return body;
    }

    // The note's content, as a body carries it.
    private static JsonObject Content(Action<JsonObject>? fill = null, JsonObject? into = null)
    {
        var body = into ?? new JsonObject { ["baseVersion"] = 0 };
        body["narrative"] = "Jane made toast.";
        body["tickedGoalIds"] = Ids();
        body["pickedGroupIds"] = Ids();
        body["tickedCommonItemIds"] = Ids();
        body["isFlagged"] = false;
        body["flagReason"] = null;
        fill?.Invoke(body);
        return body;
    }

    // The first save of a pending edit, from a page of its own, on the base given.
    private static JsonObject EditContent(Guid clientId, int seq, Action<JsonObject>? fill, int baseVersion = 1)
    {
        var body = Save(clientId, seq, null, fill);
        body["baseVersion"] = baseVersion;
        return body;
    }

    // Save changes, on the base given.
    private static JsonObject EditVersion(Action<JsonObject>? fill, int baseVersion = 1)
    {
        var body = Content(fill);
        body["baseVersion"] = baseVersion;
        return body;
    }

    private static string? ListsVersionOf(JsonElement form) => form.GetProperty("listsVersion").GetString();

    private static JsonElement ItemOf(JsonElement form, Guid id) =>
        form.GetProperty("commonItemGroups").EnumerateArray()
            .SelectMany(group => group.GetProperty("items").EnumerateArray())
            .Single(item => item.GetProperty("id").GetGuid() == id);

    private static async Task ReadCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(status));
        Assert.Equal(code, problem.RootElement.GetProperty("code").GetString());
    }

    // A 422 validation.failed whose errors name the field alone (design.md §6.1).
    private static async Task ReadValidationProblemAsync(HttpResponseMessage response, string field)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal([field], problem.RootElement.GetProperty("errors").EnumerateObject().Select(error => error.Name));
    }

    private static async Task<JsonElement> ReadFormAsync(HttpClient client, Guid participantId, DateOnly noteDate)
    {
        using var response = await client.GetAsync(
            DraftPath(participantId, noteDate), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    // Over HTTPS with the caller's antiforgery token, as the SPA autosaves (tests/README.md), for the day given, by
    // default today.
    private async Task<HttpResponseMessage> PutAsync(
        HttpClient client, Guid participantId, JsonObject body, DateOnly? noteDate = null) =>
        await client.PutWithTokenAsync(
            DraftPath(participantId, noteDate ?? Today), await client.FetchTokenAsync(), body, ifMatch: null);

    // Submit or Save changes, as the screen sends it, with a key of its own, for today's note.
    private async Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid participantId, JsonObject body)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            string.Create(
                CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{Today:yyyy-MM-dd}/versions"))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        request.Headers.Add(NoteVersionEndpoints.IdempotencyKeyHeader, Guid.NewGuid().ToString());
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // The note's first save as the form sends it, which the draft is answered 201 for.
    private async Task StartAsync(HttpClient client, Scene scene, JsonElement form, Action<JsonObject>? fill)
    {
        using var started = await PutAsync(
            client, scene.ParticipantId, Save(Guid.NewGuid(), 1, ListsVersionOf(form), fill));
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
    }

    private HttpClient ClientFor(Caller caller) => factory.CreateHttpsClient().SignInAs(caller, seeded);

    /// <summary>
    /// A participant of A's with a short term goal and a Medium or long term one; two Every note items, one that asks
    /// for details, like Other, and one that does not; and a group with one item that asks for details.
    /// </summary>
    private sealed record Scene(
        Guid ParticipantId,
        Guid ShortGoal,
        Guid LongGoal,
        Guid Other,
        Guid Plain,
        Guid Group,
        string GroupName,
        Guid GroupOther);

    private async Task<Scene> AddSceneAsync(bool hasMedicationLine = false)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A, hasMedicationLine: hasMedicationLine);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A,
            participant.Id,
            new TestGoal("Makes own breakfast", 0),
            new TestGoal("Plans a holiday", 1, IsMediumOrLongTerm: true));
        var every = await factory.Services.AddItemsAsync(
            seeded.A.OrganisationId,
            seeded.A.EveryNoteGroupId,
            new TestCommonItem("Other (staff to complete if different from above)", 0, NeedsDetail: true),
            new TestCommonItem("Plain every note item", 1));
        var groupName = $"Travel by tram {Guid.NewGuid():N}";
        var group = Assert.Single(await factory.Services.AddGroupsAsync(
            seeded.A.OrganisationId, new TestCommonItemGroup(groupName, 0)));
        var groupItem = Assert.Single(await factory.Services.AddItemsAsync(
            seeded.A.OrganisationId, group, new TestCommonItem("Other (tram)", 0, NeedsDetail: true)));
        return new(participant.Id, goals[0], goals[1], every[0], every[1], group, groupName, groupItem);
    }

    // A group with an item that no note picks, whose name is given.
    private async Task<string> AddUnpickedGroupAsync()
    {
        var name = $"Unpicked group {Guid.NewGuid():N}";
        var group = Assert.Single(await factory.Services.AddGroupsAsync(
            seeded.A.OrganisationId, new TestCommonItemGroup(name, 1)));
        await factory.Services.AddItemsAsync(seeded.A.OrganisationId, group, new TestCommonItem("Never picked", 0));
        return name;
    }

    // A change that a manager makes to the set-up between the form and the first save.
    private async Task ChangeSetUpAsync(Scene scene, string change)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            switch (change)
            {
                case "a goal's term":
                    (await db.Goals.SingleAsync(g => g.Id == scene.ShortGoal, cancellationToken))
                        .IsMediumOrLongTerm = true;
                    break;
                case "an item's need for details":
                    (await db.CommonItems.SingleAsync(i => i.Id == scene.Plain, cancellationToken))
                        .NeedsDetail = true;
                    break;
                default:
                    (await db.Participants.SingleAsync(p => p.Id == scene.ParticipantId, cancellationToken))
                        .HasMedicationLine = true;
                    break;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<T> ReadAsync<T>(Func<Grow2NotesDbContext, Task<T>> read)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await read(db);
        }
    }

    private Task<List<Note>> NotesAsync(Guid participantId) =>
        ReadAsync(db => db.Notes.AsNoTracking()
            .Where(note => note.ParticipantId == participantId)
            .ToListAsync(TestContext.Current.CancellationToken));

    private Task<NoteDraft> StoredDraftAsync(Guid participantId) =>
        ReadAsync(db => db.NoteDrafts.AsNoTracking()
            .SingleAsync(
                draft => db.Notes.Any(note => note.Id == draft.NoteId && note.ParticipantId == participantId),
                TestContext.Current.CancellationToken));

    private Task<List<NoteVersion>> VersionsAsync(Guid participantId) =>
        ReadAsync(db => db.NoteVersions.AsNoTracking()
            .Where(version => db.Notes.Any(note => note.Id == version.NoteId && note.ParticipantId == participantId))
            .OrderBy(version => version.VersionNumber)
            .ToListAsync(TestContext.Current.CancellationToken));

    private Task<List<NoteVersionItem>> VersionItemsAsync(Guid versionId) =>
        ReadAsync(db => db.NoteVersionItems.AsNoTracking()
            .Where(item => item.NoteVersionId == versionId)
            .ToListAsync(TestContext.Current.CancellationToken));
}
