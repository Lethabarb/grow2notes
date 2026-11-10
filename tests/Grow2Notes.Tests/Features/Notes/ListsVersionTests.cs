using System.Security.Cryptography;
using System.Text.Json;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Notes;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// The version of the lists that a new note's template shows (design.md §5.7, A68, A154): a goal's term, an item's need
/// for details and the Medication box count only when they are set, so a template that uses none of them hashes as it
/// did before T44 and a deploy refuses no open form. No test needs a database.
/// </summary>
public sealed class ListsVersionTests
{
    private static readonly Guid EveryNote = new("11111111-1111-1111-1111-111111111111");

    private static readonly Guid Outing = new("22222222-2222-2222-2222-222222222222");

    private static readonly Guid Goal = new("33333333-3333-3333-3333-333333333333");

    private static readonly Guid Bus = new("44444444-4444-4444-4444-444444444444");

    private static readonly Guid Other = new("55555555-5555-5555-5555-555555555555");

    [Fact]
    public void A_template_with_only_defaults_has_the_version_that_it_had_before_T44()
    {
        var template = Template();

        Assert.Equal(VersionBeforeT44(template), ListsVersion.Of(template));
    }

    // Null and false are the same to a template, as a working copy written before T44 holds null.
    [Fact]
    public void A_term_and_a_need_for_details_that_are_false_count_as_not_set()
    {
        var template = Template(isMediumOrLongTerm: false, needsDetail: false);

        Assert.Equal(ListsVersion.Of(Template()), ListsVersion.Of(template));
    }

    [Fact]
    public void A_goal_that_is_Medium_or_long_term_gives_another_version()
    {
        Assert.NotEqual(ListsVersion.Of(Template()), ListsVersion.Of(Template(isMediumOrLongTerm: true)));
    }

    [Fact]
    public void A_common_item_that_asks_for_details_gives_another_version()
    {
        Assert.NotEqual(ListsVersion.Of(Template()), ListsVersion.Of(Template(needsDetail: true)));
    }

    [Fact]
    public void A_Medication_box_gives_another_version()
    {
        Assert.NotEqual(ListsVersion.Of(Template()), ListsVersion.Of(Template(hasMedicationLine: true)));
    }

    [Fact]
    public void Each_setting_gives_a_version_of_its_own()
    {
        string[] versions =
        [
            ListsVersion.Of(Template()),
            ListsVersion.Of(Template(isMediumOrLongTerm: true)),
            ListsVersion.Of(Template(needsDetail: true)),
            ListsVersion.Of(Template(hasMedicationLine: true)),
        ];

        Assert.Equal(versions.Length, versions.Distinct().Count());
    }

    // The formula as it was before T44, copied: arrays of [ID, wording] pairs and a group's ID, name and such pairs.
    private static string VersionBeforeT44(NoteSnapshot template) =>
        Convert.ToBase64String(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            goals = template.Goals.Select(goal => new[] { goal.SourceId.ToString(), goal.Text }),
            groups = template.Groups.Select(group => new
            {
                id = group.GroupId,
                name = group.Name,
                items = template.ItemsOf(group).Select(item => new[] { item.SourceId.ToString(), item.Text }),
            }),
        })));

    // One goal, Every note with an item, and one group with an item, as a template holds them.
    private static NoteSnapshot Template(
        bool? isMediumOrLongTerm = null, bool? needsDetail = null, bool hasMedicationLine = false) =>
        new(
            [new(EveryNote, "Every note", true, false), new(Outing, "Community outing", false, false)],
            [
                new(NoteSection.Goal, Goal, null, "Makes own breakfast", false, isMediumOrLongTerm),
                new(NoteSection.CommonItem, Other, EveryNote, "Other (staff to complete)", false, null, needsDetail),
                new(NoteSection.CommonItem, Bus, Outing, "Bus fare paid", false),
            ],
            hasMedicationLine);
}
