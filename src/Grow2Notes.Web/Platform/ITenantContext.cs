namespace Grow2Notes.Web.Platform;

/// <summary>
/// The tenant: the organisation whose rows the current request or command may read and write (design.md §5.9, D3). It
/// comes from the signed-in user's session, never from the request.
/// </summary>
internal interface ITenantContext
{
    /// <summary>
    /// The organisation given to the innermost open <see cref="Use"/> block, or else the one in the signed-in user's
    /// <see cref="TenantContext.OrganisationIdClaimType"/> claim.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Neither is there, as in a request with no session outside sign-in and setup, or the claim is not a GUID.
    /// </exception>
    Guid OrganisationId { get; }

    /// <summary>
    /// Makes <paramref name="organisationId"/> the tenant until the result is disposed, in place of any claim. It is
    /// for code that runs without a session: sign-in and setup, with the organisation of the user they have loaded, and
    /// the operator commands, which run outside any request.
    /// </summary>
    IDisposable Use(Guid organisationId);
}
