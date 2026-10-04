namespace Grow2Notes.Web.Data;

/// <summary>
/// The tenant (design.md §5.3, D3). It is not tenant-owned, because it is the tenant. The <c>admin bootstrap</c> command
/// creates it; there is no sign-up. Its time zone is not stored, because it is always Australia/Melbourne (D37).
/// </summary>
internal sealed class Organisation
{
    public Guid Id { get; set; }

    /// <summary>Printed on report and export headers.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// The placeholder text in the Guided notes box (D12, D34). It is not copied into notes, because a placeholder is
    /// never part of what was written.
    /// </summary>
    public string GuidePrompts { get; set; } = "";

    public required DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// The Guide prompts screen's ETag. SQL Server sets it on every insert and update, so EF Core never writes it.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];
}
