namespace Grow2Notes.Web.Platform.Audit;

/// <summary>
/// The <c>AuditEvent.EntityType</c> values: the kinds of thing an audit event can be about, spelt as design.md §5.3
/// lists them. A test reads that list from design.md and fails unless these are exactly its names.
/// </summary>
internal static class AuditEntityTypes
{
    public const string Note = "Note";

    public const string Participant = "Participant";

    public const string Goal = "Goal";

    public const string CommonItemGroup = "CommonItemGroup";

    public const string CommonItem = "CommonItem";

    /// <summary>A user's <c>AspNetUsers</c> row, an <see cref="Data.ApplicationUser"/>.</summary>
    public const string User = "User";

    public const string Organisation = "Organisation";

    public const string Report = "Report";
}
