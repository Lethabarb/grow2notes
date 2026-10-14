namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// The tests that read what is written to standard output by putting a writer of their own in its place. Standard
/// output is the whole test process's, so xUnit runs this collection on its own, after every collection that runs in
/// parallel, and nothing else writes to it meanwhile.
/// </summary>
[CollectionDefinition(DisableParallelization = true)]
public sealed class StandardOutputCollection;
