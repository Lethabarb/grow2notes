using System.Threading.RateLimiting;
using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// The password step and the code step of a password sign-in, each limited for each account (design.md §9.9), which the
/// middleware cannot do: the account is known only inside the endpoint. The two steps have separate counts. Its counts
/// are in memory, in the one instance that the app runs (§10.1), so they are the global ones.
/// </summary>
/// <remarks>
/// The keys are never logged or put anywhere but this limiter's memory (§9.5). The password step's is taken from the
/// email already normalised, because Identity's normaliser is scoped and this class is a singleton.
/// </remarks>
internal sealed class AccountRateLimiter(RateLimitPermits permits) : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(
        key => RateLimitPartition.GetFixedWindowLimiter(key, _ => RateLimits.PerMinute(permits.Account)));

    /// <summary>
    /// Uses a permit of the account with <paramref name="normalisedEmail"/>, known or not, so that an unknown email is
    /// counted like a known one. It says whether there was one.
    /// </summary>
    public bool TryAcquirePasswordStep(string normalisedEmail)
    {
        // Trimmed because SQL Server's comparison ignores trailing spaces, so the account is found with or without them
        // (leading spaces only merge emails that find no account). Cut to the column's size because the limiter keeps
        // each key for the window and the request's email has no other bound; no account's email is longer, so the
        // cut merges only emails that find no account.
        var email = normalisedEmail.Trim();
        return TryAcquire("password:" + email[..Math.Min(email.Length, Limits.Email)]);
    }

    /// <summary>Uses a permit of the user whose password step is done, and says whether there was one.</summary>
    public bool TryAcquireCodeStep(Guid userId) => TryAcquire("code:" + userId);

    public void Dispose() => _limiter.Dispose();

    private bool TryAcquire(string key)
    {
        using var lease = _limiter.AttemptAcquire(key);
        return lease.IsAcquired;
    }
}
