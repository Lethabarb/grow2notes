namespace Grow2Notes.Web.Platform.Configuration;

/// <summary>
/// A configuration row with a <c>RowVersion</c>, which SQL Server sets on every insert and update and EF Core checks
/// as a concurrency token (design.md §5.1 <i>Concurrency</i>), such as <c>Organisation</c> for its guide prompts.
/// <see cref="RowVersionToken"/> gives its token and its ETag.
/// </summary>
internal interface IVersionedRow
{
    byte[] RowVersion { get; }
}
