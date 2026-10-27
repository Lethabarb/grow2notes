using Grow2Notes.Web.Features.Mcp;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>The one check on the text an assistant sends (mcp-server.md §5.1, A125): no database, no app.</summary>
public sealed class TextInputTests
{
    // Controls, the bidirectional controls and a tag character: one of each kind, and the ends of each range.
    public static TheoryData<int> Refused { get; } =
    [
        0x0000, 0x0007, 0x0009, 0x000A, 0x001B, 0x007F, 0x061C, 0x200E, 0x200F, 0x202A, 0x202E, 0x2066, 0x2069, 0xE0041,
    ];

    public static TheoryData<string> Allowed { get; } =
    [
        "Zoë", "Chloé Müller", "O'Brien", "O’Brien", "Smith-Jones", "Mary Anne", "‌", "‍", "­",
        "ش‌ه",
    ];

    [Theory]
    [MemberData(nameof(Refused))]
    public void Each_control_bidirectional_or_tag_character_is_refused_with_the_control_character_message(int codePoint)
    {
        var value = "Ab" + char.ConvertFromUtf32(codePoint) + "cd";

        var refusal = TextInput.Refusal("search", "Search", value, 100);

        Assert.True(TextInput.HasRefusedCharacter(value));
        Assert.NotNull(refusal);
        Assert.Equal("validation.failed", refusal.Code);
        Assert.Equal(["Search has a control or formatting character"], refusal.Fields!["search"]);
        Assert.DoesNotContain("Abcd", refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Allowed))]
    public void Accents_apostrophes_hyphens_spaces_and_the_joiners_are_allowed(string value)
    {
        Assert.False(TextInput.HasRefusedCharacter(value));
        Assert.Null(TextInput.Refusal("search", "Search", value, 100));
    }

    [Fact]
    public void A_value_of_101_characters_after_trimming_gets_the_length_message()
    {
        var refusal = TextInput.Refusal("search", "Search", new string('a', 101), 100);

        Assert.NotNull(refusal);
        Assert.Equal(["Search must be 100 characters or less"], refusal.Fields!["search"]);
    }

    [Fact]
    public void A_value_of_100_characters_with_spaces_around_it_passes()
    {
        Assert.Null(TextInput.Refusal("search", "Search", "  " + new string('a', 100) + "  ", 100));
    }

    [Fact]
    public void An_over_long_value_that_also_holds_a_control_character_gets_the_length_message()
    {
        var refusal = TextInput.Refusal("search", "Search", new string('a', 101) + "\u0007", 100);

        Assert.NotNull(refusal);
        Assert.Equal(["Search must be 100 characters or less"], refusal.Fields!["search"]);
    }

    [Fact]
    public void The_label_and_the_limit_are_the_callers()
    {
        var refusal = TextInput.Refusal("name", "Name", new string('a', 51), 50);

        Assert.NotNull(refusal);
        Assert.Equal(["Name must be 50 characters or less"], refusal.Fields!["name"]);
    }
}
