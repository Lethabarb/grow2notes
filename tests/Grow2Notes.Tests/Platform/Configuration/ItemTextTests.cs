using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Tests.Platform.Configuration;

/// <summary>
/// The one rule for a goal's and a common item's text (design.md §5.3, A6; list-editor.md *Implementation notes*):
/// cleaned to one line with its ends trimmed, then 1 to 200 characters, each kind refusing it in its own words, as its
/// screen does. The goals' and the items' endpoint tests show it through each endpoint.
/// </summary>
public sealed class ItemTextTests
{
    public static TheoryData<string?> BlankTexts { get; } = new("", "   ", "\t \r\n \n", null);

    public static TheoryData<string, string> CleanedTexts { get; } = new()
    {
        { "  Travelled by bus or train\t", "Travelled by bus or train" },
        { "Travelled by bus\nor train", "Travelled by bus or train" },
        { "Travelled by bus \r\n\r\n  or train", "Travelled by bus or train" },
        { "\nBought\rlunch\t\nat the café\n", "Bought lunch at the café" },
        { "Cooked phở  with Nguyễn", "Cooked phở  with Nguyễn" },
    };

    [Theory]
    [MemberData(nameof(CleanedTexts))]
    public void Each_line_break_with_the_spaces_around_it_becomes_one_space_and_the_ends_are_trimmed(
        string sent, string cleaned)
    {
        Assert.True(ItemText.CommonItem.TryRead(sent, out var text, out var errors));
        Assert.Equal(cleaned, text);
        Assert.Null(errors);
    }

    // 200 characters as stored, the spaces at the ends left out and the line break counted as one.
    [Fact]
    public void A_text_of_200_characters_once_cleaned_is_read()
    {
        Assert.True(ItemText.Goal.TryRead(
            $"  {new string('a', 100)} \n\n {new string('b', 99)}  ", out var text, out _));
        Assert.Equal(new string('a', 100) + " " + new string('b', 99), text);
    }

    [Theory]
    [MemberData(nameof(BlankTexts))]
    public void A_blank_or_missing_text_is_refused_in_each_kind_s_words_under_Text(string? sent)
    {
        AssertRefused(ItemText.Goal, sent, "Enter the goal");
        AssertRefused(ItemText.CommonItem, sent, "Enter the common item");
    }

    [Fact]
    public void A_text_of_201_characters_once_cleaned_is_refused_in_each_kind_s_words_under_Text()
    {
        foreach (var sent in new[]
        {
            new string('a', Limits.ItemText + 1),
            $"{new string('a', 100)}\n{new string('b', 100)}",
        })
        {
            AssertRefused(ItemText.Goal, sent, "Goal must be 200 characters or less");
            AssertRefused(ItemText.CommonItem, sent, "Common item must be 200 characters or less");
        }
    }

    private static void AssertRefused(ItemText kind, string? sent, string message)
    {
        Assert.False(kind.TryRead(sent, out var text, out var errors));
        Assert.Null(text);
        var (field, messages) = Assert.Single(errors);
        Assert.Equal("Text", field);
        Assert.Equal([message], messages);
    }
}
