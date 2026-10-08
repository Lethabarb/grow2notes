namespace Grow2Notes.Web.Data;

/// <summary>
/// A row that is only ever added, never changed or deleted, such as an audit event (design.md §5.1 <i>Append-only
/// tables</i>, §5.8). The app refuses a change or a delete in <see cref="AppendOnlySaveChangesInterceptor"/>, and the
/// database denies them to the app's identity.
/// </summary>
internal interface IAppendOnly;
