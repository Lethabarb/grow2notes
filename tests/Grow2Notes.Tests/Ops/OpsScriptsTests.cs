using Grow2Notes.Tests.Fixtures;

namespace Grow2Notes.Tests.Ops;

/// <summary>
/// How <see cref="OpsScripts"/> prepares a script of <c>ops/</c> before it connects, so none needs a database: the
/// operator's <c>sqlcmd</c> would substitute the same variables, and a value that could end a quoted literal must
/// never get as far as the script.
/// </summary>
public sealed class OpsScriptsTests
{
    private static readonly Dictionary<string, string> X = new() { ["X"] = "42" };

    [Fact]
    public void A_variable_is_replaced_wherever_the_script_has_it()
    {
        var script = OpsScripts.Substitute("SELECT '$(X)' -- $(X)", X);

        Assert.Equal("SELECT '42' -- 42", script);
    }

    [Fact]
    public void A_variable_the_script_uses_but_was_not_given_is_refused()
    {
        var error = Assert.Throws<ArgumentException>(() => OpsScripts.Substitute("SELECT '$(X)'", null));

        Assert.Contains("X", error.Message);
    }

    [Fact]
    public void A_variable_that_was_given_but_the_script_does_not_use_is_refused()
    {
        var error = Assert.Throws<ArgumentException>(() => OpsScripts.Substitute("SELECT 1", X));

        Assert.Contains("X", error.Message);
    }

    [Theory]
    [InlineData("a'b")]
    [InlineData("a b")]
    [InlineData("a;b")]
    [InlineData("a\"b")]
    [InlineData("abc\n")]
    public void A_value_with_a_character_other_than_a_letter_a_digit_or_a_hyphen_is_refused(string value)
    {
        Assert.Throws<ArgumentException>(
            () => OpsScripts.Substitute("SELECT '$(X)'", new Dictionary<string, string> { ["X"] = value }));
    }

    [Fact]
    public void An_empty_value_and_a_guid_are_accepted()
    {
        var guid = Guid.NewGuid().ToString();

        Assert.Equal("SELECT '', '" + guid + "'", OpsScripts.Substitute(
            "SELECT '$(A)', '$(B)'", new Dictionary<string, string> { ["A"] = "", ["B"] = guid }));
    }

    [Fact]
    public async Task Running_a_script_refuses_a_variable_it_does_not_use_before_connecting()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => OpsScripts.RunAsync(
            "Server=unreachable.invalid", "ops/find-ids.sql", X, TestContext.Current.CancellationToken));
    }
}
