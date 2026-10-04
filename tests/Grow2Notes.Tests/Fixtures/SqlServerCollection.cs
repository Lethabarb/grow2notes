namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// The integration tests that share one SQL Server container for the whole test run. xUnit runs the tests in a
/// collection one at a time, so they never race on the database's state.
/// </summary>
[CollectionDefinition]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>;
