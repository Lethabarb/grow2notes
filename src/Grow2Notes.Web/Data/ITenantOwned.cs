namespace Grow2Notes.Web.Data;

/// <summary>
/// A row that belongs to one organisation (design.md §5.1 <i>Tenancy</i>, D3). The tenant isolation of §5.9 keys on
/// <see cref="OrganisationId"/>: a request reads and writes only rows of its own tenant, the organisation that
/// <see cref="Platform.ITenantContext"/> gives.
/// </summary>
internal interface ITenantOwned
{
    Guid OrganisationId { get; set; }
}
