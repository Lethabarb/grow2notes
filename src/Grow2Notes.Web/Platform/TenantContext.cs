namespace Grow2Notes.Web.Platform;

/// <summary>
/// The app's <see cref="ITenantContext"/>, one per request or command scope. It reads the signed-in user from the
/// request's <see cref="HttpContext"/>, so outside a request it has only what <see cref="Use"/> gives it.
/// </summary>
internal sealed class TenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    /// <summary>
    /// The claim that holds the signed-in user's organisation ID. The claims factory adds it to the principal it builds
    /// on every request (design.md §8.4).
    /// </summary>
    public const string OrganisationIdClaimType = "org_id";

    // An AsyncLocal, so the tenant that Use sets reaches everything its block awaits or starts, on any thread, and no
    // other async flow, such as a request running at the same time. A value set inside an awaited async method does
    // not flow back to its caller, so Use belongs in the method that holds the block. Disposing puts back the value
    // from before, so blocks nest.
    private readonly AsyncLocal<Guid?> used = new();

    public Guid OrganisationId => used.Value ?? FromClaim() ?? throw new InvalidOperationException(
        $"There is no tenant: the signed-in user has no {OrganisationIdClaimType} claim and no " +
        $"{nameof(ITenantContext)}.{nameof(Use)} block is open. Code that runs without a session, such as sign-in, " +
        "setup and the operator commands, sets the tenant with Use from the user or organisation it has loaded.");

    public IDisposable Use(Guid organisationId)
    {
        var outer = used.Value;
        used.Value = organisationId;
        return new Restore(used, outer);
    }

    // Read on every access, not once: the request's DbContext, and so this, can be made during authentication, before
    // the user is set, because the security stamp check loads the user on every request (design.md §8.4).
    private Guid? FromClaim()
    {
        var claim = httpContextAccessor.HttpContext?.User.FindFirst(OrganisationIdClaimType);
        if (claim is null)
        {
            return null;
        }

        return Guid.TryParse(claim.Value, out var organisationId)
            ? organisationId
            : throw new InvalidOperationException(
                $"The signed-in user's {OrganisationIdClaimType} claim is not a GUID, so there is no tenant.");
    }

    private sealed class Restore(AsyncLocal<Guid?> used, Guid? outer) : IDisposable
    {
        public void Dispose() => used.Value = outer;
    }
}
