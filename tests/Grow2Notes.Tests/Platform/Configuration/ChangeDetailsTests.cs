using System.Text.Json.Nodes;
using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Tests.Platform.Configuration;

/// <summary>
/// The old and new values in a configuration change's audit <c>Details</c>, on stand-in values named as design.md
/// §6.6's lists name them.
/// </summary>
public sealed class ChangeDetailsTests
{
    private static readonly Guid GroupId = Guid.Parse("5d0e6f1a-2b3c-4d5e-8f90-a1b2c3d4e5f6");
    private static readonly Guid OtherGroupId = Guid.Parse("6e1f7a2b-3c4d-4e5f-9a01-b2c3d4e5f6a7");
    private static readonly DateTime ArchivedAtUtc = new(2026, 10, 13, 1, 2, 3, 456, DateTimeKind.Utc);

    // A common item moved to another group, which puts it at the end there (A42), with its wording as it was.
    [Fact]
    public void Only_the_values_that_changed_are_in_old_and_new()
    {
        var before = new JsonObject
        {
            ["text"] = "Made lunch", ["groupId"] = GroupId, ["sortOrder"] = 2, ["archivedAtUtc"] = null,
        };
        var after = new JsonObject
        {
            ["text"] = "Made lunch", ["groupId"] = OtherGroupId, ["sortOrder"] = 5, ["archivedAtUtc"] = null,
        };

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["groupId"] = GroupId, ["sortOrder"] = 2 },
            ["new"] = new JsonObject { ["groupId"] = OtherGroupId, ["sortOrder"] = 5 },
        };
        Assert.Equal(details.ToJsonString(), ChangeDetails.Of(before, after)?.ToJsonString());
    }

    [Fact]
    public void A_value_that_was_null_is_written_as_null() =>
        Assert.Equal(
            """{"old":{"archivedAtUtc":null},"new":{"archivedAtUtc":"2026-10-13T01:02:03.456Z"}}""",
            ChangeDetails.Of(
                new JsonObject { ["sortOrder"] = 3, ["archivedAtUtc"] = null },
                new JsonObject { ["sortOrder"] = 3, ["archivedAtUtc"] = ArchivedAtUtc })?.ToJsonString());

    // Separate objects with the same values, of each kind a configuration change has, compare equal as JSON.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_no_value_changed_there_are_none(bool archived)
    {
        JsonObject Values() => new()
        {
            ["text"] = "Made lunch",
            ["groupId"] = GroupId,
            ["sortOrder"] = 2,
            ["archivedAtUtc"] = archived ? ArchivedAtUtc : null,
            ["order"] = new JsonArray(GroupId, OtherGroupId),
        };

        Assert.Null(ChangeDetails.Of(Values(), Values()));
    }

    [Fact]
    public void A_new_row_has_new_alone_with_every_value_and_a_null_value_written_as_null() =>
        Assert.Equal(
            """{"new":{"text":"Walk to the shops","sortOrder":4,"archivedAtUtc":null}}""",
            ChangeDetails.Of(
                before: null,
                new JsonObject { ["text"] = "Walk to the shops", ["sortOrder"] = 4, ["archivedAtUtc"] = null })
                ?.ToJsonString());

    [Fact]
    public void An_extra_key_is_kept_beside_old_and_new() =>
        Assert.Equal(
            """{"old":{"text":"Made lunch"},"new":{"text":"Made lunch with help"},"via":"mcp"}""",
            ChangeDetails.Of(
                new JsonObject { ["text"] = "Made lunch" },
                new JsonObject { ["text"] = "Made lunch with help" },
                new JsonObject { ["via"] = "mcp" })?.ToJsonString());

    [Fact]
    public void An_extra_key_is_kept_beside_a_new_row_s_values() =>
        Assert.Equal(
            """{"new":{"text":"Made lunch"},"via":"mcp"}""",
            ChangeDetails.Of(
                before: null, new JsonObject { ["text"] = "Made lunch" }, new JsonObject { ["via"] = "mcp" })
                ?.ToJsonString());

    // Otherwise a value named on one side only would go unrecorded, or be recorded with nothing beside it.
    [Theory]
    [InlineData("""{"text":"Made lunch"}""", """{"text":"Made lunch","sortOrder":2}""")]
    [InlineData("""{"text":"Made lunch","sortOrder":2}""", """{"text":"Made lunch"}""")]
    [InlineData("""{"text":"Made lunch"}""", """{"name":"Made lunch"}""")]
    public void Values_before_and_after_that_name_different_values_are_refused(string before, string after) =>
        Assert.Throws<ArgumentException>(() =>
            ChangeDetails.Of(JsonNode.Parse(before)!.AsObject(), JsonNode.Parse(after)!.AsObject()));

    // Extra keys never make a change, so a group's reorder, which always names its group, can still change nothing.
    [Fact]
    public void Extra_keys_do_not_count_as_a_change() =>
        Assert.Null(
            ChangeDetails.Of(
                new JsonObject { ["order"] = new JsonArray(GroupId, OtherGroupId) },
                new JsonObject { ["order"] = new JsonArray(GroupId, OtherGroupId) },
                new JsonObject { ["groupId"] = GroupId, ["via"] = "mcp" }));

    // Otherwise a new row's details with an extra old key would read as a change to an existing row.
    [Theory]
    [InlineData("""{"text":"Made lunch"}""", """{"text":"Made lunch with help"}""", "old")]
    [InlineData("""{"text":"Made lunch"}""", """{"text":"Made lunch with help"}""", "new")]
    [InlineData("""{"text":"Made lunch"}""", """{"text":"Made lunch"}""", "old")]
    [InlineData(null, """{"text":"Made lunch"}""", "old")]
    [InlineData(null, """{"text":"Made lunch"}""", "new")]
    public void An_extra_key_cannot_be_named_old_or_new(string? before, string after, string key)
    {
        var refused = Assert.Throws<ArgumentException>(() =>
            ChangeDetails.Of(
                before is null ? null : JsonNode.Parse(before)!.AsObject(),
                JsonNode.Parse(after)!.AsObject(),
                new JsonObject { [key] = "Made dinner" }));
        Assert.Equal("extraDetails", refused.ParamName);
    }
}
