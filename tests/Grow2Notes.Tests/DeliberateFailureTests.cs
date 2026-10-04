namespace Grow2Notes.Tests;

// Throwaway: proves that one failing test fails the CI run.
public sealed class DeliberateFailureTests
{
    [Fact]
    public void Fails_on_purpose() => Assert.Equal(5, 2 + 2);
}
