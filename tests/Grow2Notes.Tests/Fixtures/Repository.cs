namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// The repository the tests were built from, for a test that reads its files, such as the source or design.md.
/// </summary>
internal static class Repository
{
    /// <summary>
    /// The folder that holds <c>Grow2Notes.slnx</c>. It is found from the test's build output, which is inside the
    /// repository on a machine and in CI alike, so the test needs no app host for its content root, as the tests that
    /// read the web project's files through the factory do.
    /// </summary>
    public static string Root()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Grow2Notes.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException($"No folder above {AppContext.BaseDirectory} holds Grow2Notes.slnx.");
    }
}
