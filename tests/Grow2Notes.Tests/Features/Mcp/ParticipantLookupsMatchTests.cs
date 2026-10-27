using Grow2Notes.Web.Features.Mcp;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// The rule of <c>find_participants</c> for a name that matches a search (A124): every word of the search is held,
/// ignoring case, by the given or the family name. No database, no app.
/// </summary>
public sealed class ParticipantLookupsMatchTests
{
    [Theory]
    [InlineData("jane cit", "Jane", "Citizen")]
    [InlineData("CIT jane", "Jane", "Citizen")]
    [InlineData("itiz", "Jane", "Citizen")]
    [InlineData("jones", "Alex", "Smith-Jones")]
    [InlineData("ZOË", "Zoë", "Smith")]
    public void A_name_that_holds_every_word_matches(string search, string givenName, string familyName)
    {
        Assert.True(ParticipantLookups.Matches(Words(search), givenName, familyName));
    }

    [Theory]
    [InlineData("jane x", "Jane", "Citizen")]
    [InlineData("bob", "Jane", "Citizen")]
    public void A_name_that_misses_a_word_does_not_match(string search, string givenName, string familyName)
    {
        Assert.False(ParticipantLookups.Matches(Words(search), givenName, familyName));
    }

    // Nothing is normalised (A124): the limits are recorded here so that a change to them is deliberate.
    [Theory]
    [InlineData("zoe", "Zoë", "Smith")]
    [InlineData("o’brien", "Fran", "O'Brien")]
    public void Accents_and_apostrophe_styles_are_not_folded(string search, string givenName, string familyName)
    {
        Assert.False(ParticipantLookups.Matches(Words(search), givenName, familyName));
    }

    private static string[] Words(string search) => search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
